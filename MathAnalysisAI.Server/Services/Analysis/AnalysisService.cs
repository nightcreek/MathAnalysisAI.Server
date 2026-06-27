using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Orchestration;

namespace MathAnalysisAI.Server.Services.Analysis;

public class AnalysisService : IAnalysisService
{
    private readonly IAnalysisPipeline _analysisPipeline;

    public AnalysisService(
        IAnalysisPipeline analysisPipeline)
    {
        _analysisPipeline = analysisPipeline;
    }

    public Task<AnalysisPipelineResult> AnalyzeAsync(
        AnalysisRequestDto request,
        AppUser currentUser,
        CancellationToken cancellationToken = default)
    {
        return _analysisPipeline.AnalyzeAsync(request, currentUser, cancellationToken);
    }

    public Task<AnalysisStreamPipelineResult> PrepareStreamAsync(
        AnalysisRequestDto request,
        AppUser currentUser,
        CancellationToken cancellationToken = default)
    {
        return _analysisPipeline.PrepareStreamAsync(request, currentUser, cancellationToken);
    }
}
