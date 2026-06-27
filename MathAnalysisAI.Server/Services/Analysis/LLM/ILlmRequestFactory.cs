using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Analysis.LLM
{
    public interface ILlmRequestFactory
    {
        Task<LLMChatRequestDto> BuildAsync(
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            Problem problem,
            StudentSolution? studentSolution,
            AnalysisContextDto? context,
            int analysisResultId,
            CancellationToken cancellationToken);
    }
}
