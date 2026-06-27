using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Domain;

namespace MathAnalysisAI.Server.Services.Analysis.Verification
{
    public interface IAnalysisVerificationService
    {
        Task<AnalysisVerificationResult> VerifyAsync(
            StructuredProblem? structuredProblem,
            PhotoSolutionOcrRecord? ocrRecord,
            AnalysisResult analysisResult,
            AnalysisResultModel? parsed,
            string? fallbackProblemText = null,
            string? fallbackStudentSolutionText = null,
            CancellationToken cancellationToken = default);
    }
}
