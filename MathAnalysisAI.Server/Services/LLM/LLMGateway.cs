using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using Microsoft.Extensions.Options;

namespace MathAnalysisAI.Server.Services.LLM
{
    public class LLMGateway
    {
        private const string GatewayModeDirect = "direct";
        private const string GatewayModeLiteLlm = "litellm";
        private const string ProviderDeepSeek = "deepseek";
        private const string ProviderLiteLlm = "litellm";
        private const string DefaultDeepSeekModel = "deepseek-chat";
        private const string DefaultLiteLlmAlias = "math-reviewer";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _db;
        private readonly IOptions<LLMOptions> _options;

        public LLMGateway(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ApplicationDbContext db,
            IOptions<LLMOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _db = db;
            _options = options;
        }

        public async Task<LLMChatResponseDto> ChatAsync(LLMChatRequestDto request, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new LLMChatResponseDto { IsSuccess = false };

            var settings = _options.Value;
            var timeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 60;
            var maxRetryAttempts = Math.Max(0, settings.MaxRetryAttempts);
            var maxAttempts = maxRetryAttempts + 1;
            var retryDelayMilliseconds = Math.Max(0, settings.RetryDelayMilliseconds);
            var maxErrorBodyLength = settings.MaxErrorBodyLength > 0 ? settings.MaxErrorBodyLength : 2000;

            var mode = (_configuration["LLMGateway:Mode"] ?? GatewayModeDirect).Trim().ToLowerInvariant();
            var provider = mode == GatewayModeLiteLlm ? ProviderLiteLlm : ProviderDeepSeek;
            var modelName = string.IsNullOrWhiteSpace(request.ModelName) ? DefaultDeepSeekModel : request.ModelName.Trim();
            var requestType = string.IsNullOrWhiteSpace(request.RequestType) ? "unknown" : request.RequestType.Trim();

            var status = "failed";
            string? errorCode = null;
            string? errorMessage = null;
            int? statusCode = null;
            var attemptCount = 0;
            var isRetryable = false;
            int? promptTokens = null;
            int? completionTokens = null;
            int? totalTokens = null;

            try
            {
                var messages = request.Messages
                    .Where(m => !string.IsNullOrWhiteSpace(m.Role) && !string.IsNullOrWhiteSpace(m.Content))
                    .Select(m => new { role = m.Role.Trim(), content = m.Content })
                    .ToArray();

                if (messages.Length == 0)
                {
                    errorCode = "empty_messages";
                    errorMessage = "Messages must contain at least one non-empty item.";
                    statusCode = (int)HttpStatusCode.BadRequest;
                    response.ErrorCode = errorCode;
                    response.ErrorMessage = errorMessage;
                    response.StatusCode = statusCode;
                    response.IsRetryable = false;
                    response.AttemptCount = 1;
                    status = "failed";
                    return response;
                }

                string baseUrl;
                string apiKey;
                object payload;
                if (mode == GatewayModeLiteLlm)
                {
                    baseUrl = _configuration["LiteLLM:BaseUrl"] ?? string.Empty;
                    apiKey = _configuration["LiteLLM:ApiKey"] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(baseUrl))
                    {
                        return BuildConfigFailure(response, "missing_litellm_base_url", "LiteLLM:BaseUrl is not configured.");
                    }

                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        return BuildConfigFailure(response, "missing_litellm_api_key", "LiteLLM API key is not configured. Set LiteLLM__ApiKey.");
                    }

                    if (!IsAscii(apiKey))
                    {
                        return BuildConfigFailure(response, "invalid_litellm_api_key", "LiteLLM API key contains non-ASCII characters.");
                    }

                    modelName = ResolveLiteLlmAlias(requestType, request.ModelName);
                    payload = new { model = modelName, messages };
                }
                else
                {
                    baseUrl = _configuration["DeepSeek:BaseUrl"] ?? string.Empty;
                    apiKey = _configuration["DeepSeek:ApiKey"] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(baseUrl))
                    {
                        return BuildConfigFailure(response, "missing_base_url", "DeepSeek:BaseUrl is not configured.");
                    }

                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        return BuildConfigFailure(response, "missing_api_key", "DeepSeek API key is not configured. Set DeepSeek__ApiKey.");
                    }

                    if (!IsAscii(apiKey))
                    {
                        return BuildConfigFailure(response, "invalid_api_key", "DeepSeek API key contains non-ASCII characters.");
                    }

                    modelName = string.IsNullOrWhiteSpace(request.ModelName) ? DefaultDeepSeekModel : request.ModelName.Trim();
                    payload = new { model = modelName, messages };
                }

                var retryableStatusCodes = settings.RetryOnStatusCodes?.Count > 0
                    ? new HashSet<int>(settings.RetryOnStatusCodes)
                    : new HashSet<int>([429, 502, 503, 504]);

                var client = _httpClientFactory.CreateClient();
                while (attemptCount < maxAttempts)
                {
                    attemptCount++;
                    statusCode = null;
                    isRetryable = false;

                    try
                    {
                        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        attemptCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                        };
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                        using var httpResponse = await client.SendAsync(httpRequest, attemptCts.Token);
                        statusCode = (int)httpResponse.StatusCode;

                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            var responseBody = await httpResponse.Content.ReadAsStringAsync(attemptCts.Token);
                            var truncatedBody = Truncate(responseBody, maxErrorBodyLength);
                            if (IsRetryableStatusCode(retryableStatusCodes, httpResponse.StatusCode) && attemptCount < maxAttempts)
                            {
                                errorCode = "llm_temporary_unavailable";
                                errorMessage = $"LLM provider returned HTTP {(int)httpResponse.StatusCode}.";
                                isRetryable = true;
                                await DelayBeforeRetryAsync(retryDelayMilliseconds, cancellationToken);
                                continue;
                            }

                            errorCode = MapHttpErrorCode(httpResponse.StatusCode);
                            errorMessage = string.IsNullOrWhiteSpace(truncatedBody)
                                ? $"LLM provider returned HTTP {(int)httpResponse.StatusCode}."
                                : truncatedBody;
                            statusCode = (int)httpResponse.StatusCode;
                            isRetryable = IsRetryableStatusCode(retryableStatusCodes, httpResponse.StatusCode);
                            break;
                        }

                        var responseBodySuccess = await httpResponse.Content.ReadAsStringAsync(attemptCts.Token);
                        if (!TryExtractContentAndTokens(responseBodySuccess, out var content, out promptTokens, out completionTokens, out totalTokens))
                        {
                            errorCode = mode == GatewayModeLiteLlm ? "litellm_response_parse_failed" : "response_parse_failed";
                            errorMessage = "Failed to parse LLM response.";
                            statusCode = (int)HttpStatusCode.OK;
                            isRetryable = false;
                            break;
                        }

                        response.IsSuccess = true;
                        response.Content = content;
                        response.PromptTokenCount = promptTokens;
                        response.CompletionTokenCount = completionTokens;
                        response.TotalTokenCount = totalTokens;
                        response.ErrorCode = null;
                        response.ErrorMessage = null;
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.IsRetryable = false;
                        response.AttemptCount = attemptCount;
                        status = "success";
                        return response;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        errorCode = "llm_timeout";
                        errorMessage = $"LLM request timed out after {timeoutSeconds} seconds.";
                        statusCode = (int)HttpStatusCode.GatewayTimeout;
                        isRetryable = true;

                        if (attemptCount < maxAttempts)
                        {
                            await DelayBeforeRetryAsync(retryDelayMilliseconds, cancellationToken);
                            continue;
                        }

                        break;
                    }
                    catch (HttpRequestException ex)
                    {
                        errorCode = "llm_temporary_unavailable";
                        errorMessage = Truncate(ex.Message, maxErrorBodyLength);
                        statusCode = (int)HttpStatusCode.ServiceUnavailable;
                        isRetryable = true;

                        if (attemptCount < maxAttempts)
                        {
                            await DelayBeforeRetryAsync(retryDelayMilliseconds, cancellationToken);
                            continue;
                        }

                        break;
                    }
                }

                response.ErrorCode = errorCode;
                response.ErrorMessage = errorMessage;
                response.StatusCode = statusCode;
                response.IsRetryable = isRetryable;
                response.AttemptCount = Math.Max(1, attemptCount);
                status = "failed";
                return response;
            }
            catch (OperationCanceledException)
            {
                errorCode = "request_canceled";
                errorMessage = "LLM request canceled.";
                response.ErrorCode = errorCode;
                response.ErrorMessage = errorMessage;
                response.StatusCode = statusCode;
                response.IsRetryable = false;
                response.AttemptCount = Math.Max(1, attemptCount);
                status = "failed";
                return response;
            }
            catch (Exception ex)
            {
                errorCode = "gateway_exception";
                errorMessage = Truncate(ex.Message, maxErrorBodyLength);
                response.ErrorCode = errorCode;
                response.ErrorMessage = errorMessage;
                response.StatusCode = statusCode;
                response.IsRetryable = false;
                response.AttemptCount = Math.Max(1, attemptCount);
                status = "failed";
                return response;
            }
            finally
            {
                stopwatch.Stop();
                response.LatencyMs = stopwatch.ElapsedMilliseconds;
                response.AttemptCount = Math.Max(1, response.AttemptCount);

                var log = new LLMRequestLog
                {
                    Provider = provider,
                    ModelName = modelName,
                    RequestType = requestType,
                    PromptTokenCount = promptTokens,
                    CompletionTokenCount = completionTokens,
                    TotalTokenCount = totalTokens,
                    LatencyMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
                    AttemptCount = response.AttemptCount,
                    StatusCode = response.StatusCode,
                    Status = status,
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    UserId = request.UserId,
                    AnalysisResultId = request.AnalysisResultId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.LLMRequestLogs.Add(log);
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    // Logging failures should not mask the original LLM response.
                }
            }
        }

        public async IAsyncEnumerable<string> StreamChatAsync(
            LLMChatRequestDto request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = _options.Value;
            var timeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 60;

            var mode = (_configuration["LLMGateway:Mode"] ?? GatewayModeDirect).Trim().ToLowerInvariant();
            var modelName = string.IsNullOrWhiteSpace(request.ModelName) ? DefaultDeepSeekModel : request.ModelName.Trim();
            var requestType = string.IsNullOrWhiteSpace(request.RequestType) ? "unknown" : request.RequestType.Trim();

            var messages = request.Messages
                .Where(m => !string.IsNullOrWhiteSpace(m.Role) && !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => new { role = m.Role.Trim(), content = m.Content })
                .ToArray();

            if (messages.Length == 0)
            {
                yield break;
            }

            string baseUrl;
            string apiKey;
            object payload;
            if (mode == GatewayModeLiteLlm)
            {
                baseUrl = _configuration["LiteLLM:BaseUrl"] ?? string.Empty;
                apiKey = _configuration["LiteLLM:ApiKey"] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
                    yield break;

                modelName = ResolveLiteLlmAlias(requestType, request.ModelName);
                payload = new { model = modelName, messages, stream = true };
            }
            else
            {
                baseUrl = _configuration["DeepSeek:BaseUrl"] ?? string.Empty;
                apiKey = _configuration["DeepSeek:ApiKey"] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
                    yield break;

                modelName = string.IsNullOrWhiteSpace(request.ModelName) ? DefaultDeepSeekModel : request.ModelName.Trim();
                payload = new { model = modelName, messages, stream = true };
            }

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds * 2);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                yield break;
            }

            using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("data: "))
                {
                    var data = line["data: ".Length..].Trim();
                    if (data == "[DONE]")
                        yield break;

                    string? text = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(data);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var delta = choices[0].GetProperty("delta");
                            if (delta.TryGetProperty("content", out var content))
                            {
                                text = content.GetString();
                            }
                        }
                    }
                    catch (JsonException)
                    {
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        yield return text;
                    }
                }
            }
        }

        private static LLMChatResponseDto BuildConfigFailure(LLMChatResponseDto response, string errorCode, string errorMessage)
        {
            response.IsSuccess = false;
            response.ErrorCode = errorCode;
            response.ErrorMessage = errorMessage;
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.IsRetryable = false;
            response.AttemptCount = 1;
            return response;
        }

        private static string MapHttpErrorCode(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.Unauthorized => "llm_auth_error",
                HttpStatusCode.Forbidden => "llm_auth_error",
                HttpStatusCode.BadRequest => "llm_request_failed",
                _ => "llm_temporary_unavailable"
            };
        }

        private static bool IsRetryableStatusCode(HashSet<int> retryableStatusCodes, HttpStatusCode statusCode)
        {
            return retryableStatusCodes.Contains((int)statusCode);
        }

        private static async Task DelayBeforeRetryAsync(int retryDelayMilliseconds, CancellationToken cancellationToken)
        {
            if (retryDelayMilliseconds <= 0)
            {
                return;
            }

            await Task.Delay(retryDelayMilliseconds, cancellationToken);
        }

        private string ResolveLiteLlmAlias(string requestType, string? requestedModelName)
        {
            var mapped = _configuration[$"LLMGateway:RequestTypeModelMap:{requestType}"];
            if (!string.IsNullOrWhiteSpace(mapped))
            {
                return mapped.Trim();
            }

            if (!string.IsNullOrWhiteSpace(requestedModelName))
            {
                return requestedModelName.Trim();
            }

            return DefaultLiteLlmAlias;
        }

        private static bool IsAscii(string value)
        {
            return value.All(c => c <= sbyte.MaxValue);
        }

        private static bool TryExtractContentAndTokens(
            string responseBody,
            out string? content,
            out int? promptTokens,
            out int? completionTokens,
            out int? totalTokens)
        {
            content = null;
            promptTokens = null;
            completionTokens = null;
            totalTokens = null;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(responseBody);
            }
            catch
            {
                return false;
            }

            using (doc)
            {
                if (doc.RootElement.TryGetProperty("choices", out var choices)
                    && choices.ValueKind == JsonValueKind.Array
                    && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("message", out var message)
                        && message.TryGetProperty("content", out var contentElement)
                        && contentElement.ValueKind == JsonValueKind.String)
                    {
                        content = contentElement.GetString();
                    }
                }

                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("prompt_tokens", out var prompt)
                        && prompt.ValueKind == JsonValueKind.Number
                        && prompt.TryGetInt32(out var p))
                    {
                        promptTokens = p;
                    }

                    if (usage.TryGetProperty("completion_tokens", out var completion)
                        && completion.ValueKind == JsonValueKind.Number
                        && completion.TryGetInt32(out var c))
                    {
                        completionTokens = c;
                    }

                    if (usage.TryGetProperty("total_tokens", out var total)
                        && total.ValueKind == JsonValueKind.Number
                        && total.TryGetInt32(out var t))
                    {
                        totalTokens = t;
                    }
                }
            }

            return !string.IsNullOrWhiteSpace(content);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }
    }
}
