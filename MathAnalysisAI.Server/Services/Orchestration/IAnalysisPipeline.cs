using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Orchestration;

public interface IAnalysisPipeline
{
    Task<AnalysisPipelineResult> AnalyzeAsync(
        AnalysisRequestDto request,
        AppUser currentUser,
        CancellationToken cancellationToken = default);

    Task<AnalysisStreamPipelineResult> PrepareStreamAsync(
        AnalysisRequestDto request,
        AppUser currentUser,
        CancellationToken cancellationToken = default);
}

public sealed class AnalysisPipelineResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Message { get; init; }
    public int? OcrRecordId { get; init; }
    public string? OcrStatus { get; init; }
    public AnalysisResponseDto? Response { get; init; }

    public static AnalysisPipelineResult Succeeded(AnalysisResponseDto response) => new()
    {
        Success = true,
        StatusCode = StatusCodes.Status200OK,
        Response = response
    };

    public static AnalysisPipelineResult Failed(
        int statusCode,
        string? message,
        int? ocrRecordId = null,
        string? ocrStatus = null) => new()
    {
        Success = false,
        StatusCode = statusCode,
        Message = message,
        OcrRecordId = ocrRecordId,
        OcrStatus = ocrStatus
    };
}

public sealed class AnalysisStreamPipelineResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Message { get; init; }
    public int? OcrRecordId { get; init; }
    public string? OcrStatus { get; init; }
    public IAsyncEnumerable<string>? Stream { get; init; }

    public static AnalysisStreamPipelineResult Succeeded(IAsyncEnumerable<string> stream) => new()
    {
        Success = true,
        StatusCode = StatusCodes.Status200OK,
        Stream = stream
    };

    public static AnalysisStreamPipelineResult Failed(
        int statusCode,
        string? message,
        int? ocrRecordId = null,
        string? ocrStatus = null) => new()
    {
        Success = false,
        StatusCode = statusCode,
        Message = message,
        OcrRecordId = ocrRecordId,
        OcrStatus = ocrStatus
    };
}
