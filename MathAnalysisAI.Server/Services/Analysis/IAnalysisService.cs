using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Orchestration;

namespace MathAnalysisAI.Server.Services.Analysis;

public interface IAnalysisService
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
