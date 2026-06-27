using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Analysis.Fallback
{
    public interface IAnalysisFallbackService
    {
        void ApplyFallbacks(
            AnalysisUao parsed,
            string analysisMode,
            string problemText,
            string? studentSolutionText,
            string? rawLlmContent,
            string? chapterName);
    }
}
