namespace MathAnalysisAI.Server.DTOs.LLM
{
    public class LLMChatResponseDto
    {
        public bool IsSuccess { get; set; }
        public string? Content { get; set; }
        public string? ErrorCode { get; set; }
        public bool IsRetryable { get; set; }
        public int? StatusCode { get; set; }
        public int AttemptCount { get; set; }
        public string? ErrorMessage { get; set; }
        public int? PromptTokenCount { get; set; }
        public int? CompletionTokenCount { get; set; }
        public int? TotalTokenCount { get; set; }
        public long LatencyMs { get; set; }
    }
}
