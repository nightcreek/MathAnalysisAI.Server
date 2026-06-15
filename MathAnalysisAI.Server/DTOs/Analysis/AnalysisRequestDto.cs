using System.ComponentModel.DataAnnotations;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;

namespace MathAnalysisAI.Server.DTOs.Analysis
{
    public class AnalysisRequestDto
    {
        [Required]
        public int CourseId { get; set; }

        public int? ChapterId { get; set; }

        [Required]
        public string ProblemText { get; set; } = string.Empty;

        public string? StudentSolutionText { get; set; }

        [Required]
        public string AnalysisMode { get; set; } = string.Empty;

        public int? UserId { get; set; }

        public int? OcrRecordId { get; set; }

        public int? StructuredProblemId { get; set; }

        public List<FormulaCandidateDto>? Formulas { get; set; }
    }
}
