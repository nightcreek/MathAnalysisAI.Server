using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.SharedKernel.Analysis;

namespace MathAnalysisAI.Server.Services.Visualization;

public interface IVisualizationService
{
    Task<AnalysisVisualization?> SaveVisualizationAsync(
        int analysisResultId,
        VisualizationSpec visualization,
        CancellationToken cancellationToken = default);
}
