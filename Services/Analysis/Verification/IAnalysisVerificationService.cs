using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Analysis.Verification
{
    public interface IAnalysisVerificationService
    {
        Task<AnalysisVerificationResult> VerifyAsync(
            StructuredProblem? structuredProblem,
            PhotoSolutionOcrRecord? ocrRecord,
            AnalysisResult analysisResult,
            AnalysisResponseDto? parsed,
            string? fallbackProblemText = null,
            string? fallbackStudentSolutionText = null,
            CancellationToken cancellationToken = default);
    }
}
