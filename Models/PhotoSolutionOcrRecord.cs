using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Models
{
    [Index(nameof(UserId), nameof(UploadedAt), Name = "IX_PhotoSolutionOcrRecords_UserId_UploadedAt")]
    [Index(nameof(UserId), nameof(Status), Name = "IX_PhotoSolutionOcrRecords_UserId_Status")]
    [Index(nameof(ImageHash), Name = "IX_PhotoSolutionOcrRecords_ImageHash")]
    public class PhotoSolutionOcrRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int CourseId { get; set; }

        public int? ChapterId { get; set; }

        [Required]
        [MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public long FileSizeBytes { get; set; }

        [Required]
        [MaxLength(64)]
        public string ImageHash { get; set; } = string.Empty;

        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(32)]
        public string OcrProvider { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? OcrModelName { get; set; }

        public string? RecognizedProblemText { get; set; }
        public string? RecognizedStudentSolutionText { get; set; }
        public string? DetectedSectionsJson { get; set; }
        public string? FormulasJson { get; set; }
        public string? WarningsJson { get; set; }
        public string? ReviewReasonsJson { get; set; }
        public decimal? Confidence { get; set; }

        [Required]
        [MaxLength(32)]
        public string Status { get; set; } = PhotoSolutionOcrRecordStatus.PendingReview;

        public string? ConfirmedProblemText { get; set; }
        public string? ConfirmedStudentSolutionText { get; set; }
        public string? ConfirmedFormulasJson { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public int? ConfirmedByUserId { get; set; }

        public AppUser? User { get; set; }
        public AppUser? ConfirmedByUser { get; set; }
        public ICollection<StructuredProblem> StructuredProblems { get; set; } = new List<StructuredProblem>();
        public ICollection<Problem> Problems { get; set; } = new List<Problem>();
        public ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();
    }
}
