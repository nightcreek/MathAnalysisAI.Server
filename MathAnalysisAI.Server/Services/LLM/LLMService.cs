using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.LLM;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.LLM;

public sealed class LLMService : ILLMService
{
    private readonly ILlmRequestFactory _llmRequestFactory;
    private readonly ILLMModule _llmGateway;

    public LLMService(
        ILlmRequestFactory llmRequestFactory,
        ILLMModule llmGateway)
    {
        _llmRequestFactory = llmRequestFactory;
        _llmGateway = llmGateway;
    }

    public async Task<LLMChatRequestDto> BuildAnalysisRequestAsync(
        UAOInputModel request,
        Course course,
        Chapter? chapter,
        Problem problem,
        StudentSolution? studentSolution,
        AnalysisContextDto? context,
        int analysisResultId,
        CancellationToken cancellationToken = default)
    {
        return await _llmRequestFactory.BuildAsync(
            request,
            course,
            chapter,
            problem,
            studentSolution,
            context,
            analysisResultId,
            cancellationToken);
    }

    public async Task<LLMChatResponseDto> ExecuteAnalysisAsync(
        UAOInputModel request,
        Course course,
        Chapter? chapter,
        Problem problem,
        StudentSolution? studentSolution,
        AnalysisContextDto? context,
        int analysisResultId,
        CancellationToken cancellationToken = default)
    {
        var llmRequest = await BuildAnalysisRequestAsync(
            request,
            course,
            chapter,
            problem,
            studentSolution,
            context,
            analysisResultId,
            cancellationToken);

        return await _llmGateway.ChatAsync(llmRequest, cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamAnalysisAsync(
        LLMChatRequestDto llmRequest,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in _llmGateway.StreamChatAsync(llmRequest, cancellationToken))
        {
            yield return chunk;
        }
    }
}
