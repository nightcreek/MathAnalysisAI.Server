using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Analysis.Structuring
{
    public interface IProblemStructuringService
    {
        Task<StructuredProblem> CreateFromManualInputAsync(
            UAOInputModel request,
            int userId,
            CancellationToken cancellationToken = default);

        Task<StructuredProblem> CreateFromConfirmedOcrAsync(
            PhotoSolutionOcrRecord ocrRecord,
            UAOInputModel request,
            int userId,
            CancellationToken cancellationToken = default);
    }
}
