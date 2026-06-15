using System.Net.Http;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Controllers;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.LLM;
using MathAnalysisAI.Server.Services.OCR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class ModelCallStabilityTests
{
    [Fact]
    public async Task LlmTimeout_ShouldReturnControlledError()
    {
        var db = TestDb.Create(nameof(LlmTimeout_ShouldReturnControlledError));
        var handler = new SequencedHttpMessageHandler([
            async (_, cancellationToken) =>
            {
                await Task.Delay(2000, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"choices":[{"message":{"content":"{\"ok\":true}"}}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""")
                };
            }
        ]);

        var gateway = CreateGateway(db, handler, new LLMOptions { TimeoutSeconds = 1, MaxRetryAttempts = 0, RetryDelayMilliseconds = 0 });
        var result = await gateway.ChatAsync(CreateLlmRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("llm_timeout", result.ErrorCode);
        Assert.True(result.IsRetryable);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal((int)HttpStatusCode.GatewayTimeout, result.StatusCode);
    }

    [Fact]
    public async Task LlmRetryableStatus_ShouldRetryAndThenSucceed()
    {
        var db = TestDb.Create(nameof(LlmRetryableStatus_ShouldRetryAndThenSucceed));
        var handler = new SequencedHttpMessageHandler([
            (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("temporary unavailable")
            }),
            (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"{\"ok\":true}"}}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""")
            })
        ]);

        var gateway = CreateGateway(db, handler, new LLMOptions { TimeoutSeconds = 1, MaxRetryAttempts = 2, RetryDelayMilliseconds = 0 });
        var result = await gateway.ChatAsync(CreateLlmRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.AttemptCount);
        Assert.Null(result.ErrorCode);
        Assert.Equal("{\"ok\":true}", result.Content);
    }

    [Fact]
    public async Task Llm401_ShouldNotRetry()
    {
        var db = TestDb.Create(nameof(Llm401_ShouldNotRetry));
        var handler = new SequencedHttpMessageHandler([
            (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("unauthorized")
            })
        ]);

        var gateway = CreateGateway(db, handler, new LLMOptions { TimeoutSeconds = 1, MaxRetryAttempts = 2, RetryDelayMilliseconds = 0 });
        var result = await gateway.ChatAsync(CreateLlmRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal("llm_auth_error", result.ErrorCode);
        Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public async Task Llm403_ShouldNotRetry()
    {
        var db = TestDb.Create(nameof(Llm403_ShouldNotRetry));
        var handler = new SequencedHttpMessageHandler([
            (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("forbidden")
            })
        ]);

        var gateway = CreateGateway(db, handler, new LLMOptions { TimeoutSeconds = 1, MaxRetryAttempts = 2, RetryDelayMilliseconds = 0 });
        var result = await gateway.ChatAsync(CreateLlmRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal("llm_auth_error", result.ErrorCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public async Task LlmRetryExhausted_ShouldReturnStructuredError()
    {
        var db = TestDb.Create(nameof(LlmRetryExhausted_ShouldReturnStructuredError));
        var handler = new SequencedHttpMessageHandler([
            (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("bad gateway") }),
            (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("bad gateway") })
        ]);

        var gateway = CreateGateway(db, handler, new LLMOptions { TimeoutSeconds = 1, MaxRetryAttempts = 1, RetryDelayMilliseconds = 0 });
        var result = await gateway.ChatAsync(CreateLlmRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal("llm_temporary_unavailable", result.ErrorCode);
        Assert.True(result.IsRetryable);
        Assert.Equal((int)HttpStatusCode.BadGateway, result.StatusCode);
    }

    [Fact]
    public async Task Llm429_ShouldRetryAndThenSucceed()
    {
        var db = TestDb.Create(nameof(Llm429_ShouldRetryAndThenSucceed));
        var handler = new SequencedHttpMessageHandler([
            (_, __) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("rate limited")
            }),
            (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"{\"ok\":true}"}}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""")
            })
        ]);

        var gateway = CreateGateway(db, handler, new LLMOptions { TimeoutSeconds = 1, MaxRetryAttempts = 2, RetryDelayMilliseconds = 0 });
        var result = await gateway.ChatAsync(CreateLlmRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.AttemptCount);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task OcrTimeout_ShouldReturnFailureWithoutHanging()
    {
        var provider = CreateOcrProvider(
            new SequencedHttpMessageHandler([
                async (_, cancellationToken) =>
                {
                    await Task.Delay(2000, cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"choices":[{"message":{"content":"{\"problemText\":\"题目\",\"studentSolutionText\":\"解答\",\"detectedSections\":[],\"formulas\":[],\"warnings\":[],\"confidence\":0.98}"}}]}""")
                    };
                }
            ]),
            new PhotoSolutionOcrOptions { TimeoutSeconds = 1, MaxRetryAttempts = 0, RetryDelayMilliseconds = 0 });

        var result = await provider.RecognizeAsync(CreateOcrRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("ocr_timeout", result.ErrorCode);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal((int)HttpStatusCode.GatewayTimeout, result.StatusCode);
    }

    [Fact]
    public async Task OcrRetryThenSuccess_ShouldReturnSuccess()
    {
        var provider = CreateOcrProvider(
            new SequencedHttpMessageHandler([
                (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("temporary unavailable")
                }),
                (_, __) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"choices":[{"message":{"content":"{\"problemText\":\"题目\",\"studentSolutionText\":\"解答\",\"detectedSections\":[],\"formulas\":[],\"warnings\":[],\"confidence\":0.98}"}}]}""")
                })
            ]),
            new PhotoSolutionOcrOptions { TimeoutSeconds = 1, MaxRetryAttempts = 2, RetryDelayMilliseconds = 0 });

        var result = await provider.RecognizeAsync(CreateOcrRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.AttemptCount);
        Assert.Null(result.ErrorCode);
        Assert.Equal("题目", result.ProblemText);
    }

    [Fact]
    public async Task OcrFinalFailure_ShouldNotFakeSuccess()
    {
        var db = TestDb.Create(nameof(OcrFinalFailure_ShouldNotFakeSuccess));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var controller = TestServiceFactory.CreatePhotoSolutionsController(db, new FailingPhotoSolutionOcrProvider(), user);

        var result = await controller.Ocr(course.Id, null, null, TestServiceFactory.CreateFormFile(), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.BadGateway, status.StatusCode);
        Assert.Empty(db.PhotoSolutionOcrRecords);
    }

    private static LLMGateway CreateGateway(ApplicationDbContext db, HttpMessageHandler handler, LLMOptions options)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DeepSeek:BaseUrl"] = "http://127.0.0.1/fake",
            ["DeepSeek:ApiKey"] = "test-key"
        }).Build();

        return new LLMGateway(new SingleClientFactory(handler), config, db, Microsoft.Extensions.Options.Options.Create(options));
    }

    private static LiteLLMPhotoSolutionOcrProvider CreateOcrProvider(HttpMessageHandler handler, PhotoSolutionOcrOptions options)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LiteLLM:BaseUrl"] = "http://127.0.0.1/fake",
            ["LiteLLM:ApiKey"] = "test-key",
            ["PhotoSolutionOcr:ModelAlias"] = "photo-solution-ocr",
            ["PhotoSolutionOcr:MaxImageBytes"] = "10485760"
        }).Build();

        return new LiteLLMPhotoSolutionOcrProvider(
            new SingleClientFactory(handler),
            config,
            NullLogger<LiteLLMPhotoSolutionOcrProvider>.Instance,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    private static LLMChatRequestDto CreateLlmRequest()
    {
        return new LLMChatRequestDto
        {
            UserId = 1,
            RequestType = "review_solution",
            Messages =
            {
                new LLMChatMessageDto { Role = "user", Content = "请解答这道题" }
            }
        };
    }

    private static PhotoSolutionOcrRequest CreateOcrRequest()
    {
        return new PhotoSolutionOcrRequest
        {
            CourseId = 1001,
            FileName = "sample.png",
            ContentType = "image/png",
            ImageBytes = [1, 2, 3, 4]
        };
    }

    private sealed class FailingPhotoSolutionOcrProvider : IPhotoSolutionOcrProvider
    {
        public Task<PhotoSolutionOcrResponseDto> RecognizeAsync(PhotoSolutionOcrRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PhotoSolutionOcrResponseDto
            {
                IsSuccess = false,
                ErrorCode = "ocr_temporary_unavailable",
                ErrorMessage = "OCR provider failed after retries.",
                IsRetryable = true,
                StatusCode = (int)HttpStatusCode.BadGateway,
                AttemptCount = 3,
                RawProvider = "litellm",
                ModelName = "photo-solution-ocr"
            });
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public SingleClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class SequencedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _steps;

        public SequencedHttpMessageHandler(IEnumerable<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> steps)
        {
            _steps = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(steps);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_steps.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("no more steps configured")
                });
            }

            var step = _steps.Dequeue();
            return step(request, cancellationToken);
        }
    }
}
