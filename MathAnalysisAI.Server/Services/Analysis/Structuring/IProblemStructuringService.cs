using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Analysis.Structuring
{
    public interface IProblemStructuringService
    {
        Task<StructuredProblem> CreateFromManualInputAsync(
            AnalysisRequestDto request,
            int userId,
            CancellationToken cancellationToken = default);

        Task<StructuredProblem> CreateFromConfirmedOcrAsync(
            PhotoSolutionOcrRecord ocrRecord,
            AnalysisRequestDto request,
            int userId,
            CancellationToken cancellationToken = default);
    }
}
