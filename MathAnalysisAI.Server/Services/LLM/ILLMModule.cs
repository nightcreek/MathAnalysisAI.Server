using MathAnalysisAI.Server.DTOs.LLM;

namespace MathAnalysisAI.Server.Services.LLM;

public interface ILLMModule
{
    Task<LLMChatResponseDto> ChatAsync(
        LLMChatRequestDto request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamChatAsync(
        LLMChatRequestDto request,
        CancellationToken cancellationToken = default);
}
