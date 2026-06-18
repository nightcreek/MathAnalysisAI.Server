namespace MathAnalysisAI.Server.Services.ExceptionHandling;

public sealed class ApiErrorResponse
{
    public string ErrorCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public bool IsRetryable { get; init; }
}
