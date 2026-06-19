using Microsoft.AspNetCore.Diagnostics;

namespace MathAnalysisAI.Server.Services.ExceptionHandling;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, errorCode, message, isRetryable) = ApiExceptionClassifier.Classify(exception);
        var traceId = httpContext.TraceIdentifier;

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred. Path={Path}, Method={Method}, TraceId={TraceId}",
                httpContext.Request.Path, httpContext.Request.Method, traceId);
        }
        else
        {
            _logger.LogWarning(exception, "Request failed. Path={Path}, Method={Method}, TraceId={TraceId}",
                httpContext.Request.Path, httpContext.Request.Method, traceId);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.Headers["X-Trace-Id"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            ErrorCode = errorCode,
            Message = message,
            TraceId = traceId,
            IsRetryable = isRetryable
        }, cancellationToken);
        return true;
    }
}
