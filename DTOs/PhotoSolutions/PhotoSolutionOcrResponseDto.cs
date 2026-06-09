using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.DTOs.PhotoSolutions
{
    public class PhotoSolutionOcrResponseDto
    {
        public bool IsSuccess { get; set; } = true;
        public int? OcrRecordId { get; set; }
        public string ProblemText { get; set; } = string.Empty;
        public string StudentSolutionText { get; set; } = string.Empty;
        public List<DetectedSectionDto> DetectedSections { get; set; } = new();
        public List<FormulaCandidateDto> Formulas { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> ReviewReasons { get; set; } = new();
        public decimal? Confidence { get; set; }
        public string Status { get; set; } = PhotoSolutionOcrRecordStatus.PendingReview;
        public bool NeedsManualReview { get; set; }
        public bool IsConfirmed { get; set; }
        public bool CanAnalyze { get; set; }
        public string? ErrorCode { get; set; }
        public bool IsRetryable { get; set; }
        public int? StatusCode { get; set; }
        public int AttemptCount { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RawProvider { get; set; }
        public string? ModelName { get; set; }
    }
}
