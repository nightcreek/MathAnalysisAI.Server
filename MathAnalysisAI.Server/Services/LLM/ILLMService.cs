using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.LLM;

public interface ILLMService
{
    Task<LLMChatRequestDto> BuildAnalysisRequestAsync(
        UAOInputModel request,
        Course course,
        Chapter? chapter,
        Problem problem,
        StudentSolution? studentSolution,
        AnalysisContextDto? context,
        int analysisResultId,
        CancellationToken cancellationToken = default);

    Task<LLMChatResponseDto> ExecuteAnalysisAsync(
        UAOInputModel request,
        Course course,
        Chapter? chapter,
        Problem problem,
        StudentSolution? studentSolution,
        AnalysisContextDto? context,
        int analysisResultId,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAnalysisAsync(
        LLMChatRequestDto llmRequest,
        CancellationToken cancellationToken = default);
}
