using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Services.ExceptionHandling;

public static class ControllerApiResponseExtensions
{
    public static ObjectResult ApiError(
        this ControllerBase controller,
        int statusCode,
        string errorCode,
        string message,
        bool isRetryable = false)
    {
        var traceId = controller.HttpContext.TraceIdentifier;
        controller.Response.Headers["X-Trace-Id"] = traceId;

        return new ObjectResult(new ApiErrorResponse
        {
            ErrorCode = errorCode,
            Message = message,
            TraceId = traceId,
            IsRetryable = isRetryable
        })
        {
            StatusCode = statusCode
        };
    }

    public static IActionResult DegradedOk<T>(this ControllerBase controller, T payload)
    {
        controller.Response.Headers["X-Degraded-Response"] = "true";
        controller.Response.Headers["X-Trace-Id"] = controller.HttpContext.TraceIdentifier;
        return controller.Ok(payload);
    }
}
