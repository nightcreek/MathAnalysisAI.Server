using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Orchestration;

public sealed class AnalysisRequestPreparationResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Message { get; init; }
    public int? OcrRecordId { get; init; }
    public string? OcrStatus { get; init; }
    public UAOInputModel? PreparedInput { get; init; }

    public static AnalysisRequestPreparationResult Succeeded(UAOInputModel preparedInput) => new()
    {
        Success = true,
        StatusCode = StatusCodes.Status200OK,
        PreparedInput = preparedInput
    };

    public static AnalysisRequestPreparationResult NotFound(string message) => new()
    {
        Success = false,
        StatusCode = StatusCodes.Status404NotFound,
        Message = message
    };

    public static AnalysisRequestPreparationResult Forbidden(string message) => new()
    {
        Success = false,
        StatusCode = StatusCodes.Status403Forbidden,
        Message = message
    };

    public static AnalysisRequestPreparationResult Conflict(string message, int? ocrRecordId = null, string? ocrStatus = null) => new()
    {
        Success = false,
        StatusCode = StatusCodes.Status409Conflict,
        Message = message,
        OcrRecordId = ocrRecordId,
        OcrStatus = ocrStatus
    };
}
