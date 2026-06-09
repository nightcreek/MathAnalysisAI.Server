using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Analysis.Verification
{
    public sealed class AnalysisVerificationResult
    {
        public AnswerReliability AnswerReliability { get; init; }
        public bool NeedsReview { get; init; }
        public List<string> ReliabilityReasons { get; init; } = new();
        public List<string> VerifierWarnings { get; init; } = new();
        public DateTime VerifiedAt { get; init; }
    }
}
