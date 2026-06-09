using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Models
{
    [Index(nameof(PhotoSolutionOcrRecordId), Name = "IX_StructuredProblems_PhotoSolutionOcrRecordId")]
    [Index(nameof(CreatedByUserId), Name = "IX_StructuredProblems_CreatedByUserId")]
    [Index(nameof(Status), Name = "IX_StructuredProblems_Status")]
    public class StructuredProblem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public StructuredProblemSourceType SourceType { get; set; }

        [Required]
        public string RawProblemText { get; set; } = string.Empty;

        [Required]
        public string NormalizedProblemText { get; set; } = string.Empty;

        public string? StudentSolutionText { get; set; }
        public string? FormulasJson { get; set; }
        public string? GivenConditionsJson { get; set; }
        public string? TargetText { get; set; }

        [MaxLength(64)]
        public string? ProblemType { get; set; }

        public string? KnowledgePointCandidatesJson { get; set; }
        public decimal? Confidence { get; set; }

        [Required]
        public StructuredProblemStatus Status { get; set; }

        public int? PhotoSolutionOcrRecordId { get; set; }

        [Required]
        public int CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public PhotoSolutionOcrRecord? PhotoSolutionOcrRecord { get; set; }
        public AppUser? CreatedByUser { get; set; }
        public ICollection<Problem> Problems { get; set; } = new List<Problem>();
        public ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();
    }
}
