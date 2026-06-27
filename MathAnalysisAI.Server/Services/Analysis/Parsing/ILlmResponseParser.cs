using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Analysis.Parsing
{
    public interface ILlmResponseParser
    {
        LlmParseResult Parse(string? rawContent);
    }

    public sealed class LlmParseResult
    {
        public bool Success { get; init; }
        public AnalysisUao? Parsed { get; init; }
        public string? ErrorMessage { get; init; }
        public string? NormalizedJson { get; init; }
    }
}
