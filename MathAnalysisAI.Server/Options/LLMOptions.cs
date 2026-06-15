namespace MathAnalysisAI.Server.Options;

public sealed class LLMOptions
{
    public const string SectionName = "LLMGateway";

    public int TimeoutSeconds { get; set; } = 60;
    public int MaxRetryAttempts { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 800;
    public List<int> RetryOnStatusCodes { get; set; } = [429, 502, 503, 504];
    public int MaxErrorBodyLength { get; set; } = 2000;
}
