using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MathAnalysisAI.Server.Models;

public class Question
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? StandardAnswer { get; set; }

    public string? SolutionHint { get; set; }

    [MaxLength(32)]
    public string Difficulty { get; set; } = "medium";

    [MaxLength(32)]
    public string QuestionType { get; set; } = "calculation";

    [ForeignKey(nameof(Course))]
    public int? CourseId { get; set; }

    public Course? Course { get; set; }

    [ForeignKey(nameof(Chapter))]
    public int? ChapterId { get; set; }

    public Chapter? Chapter { get; set; }

    [ForeignKey(nameof(KnowledgePoint))]
    public int? PrimaryKnowledgePointId { get; set; }

    public KnowledgePoint? KnowledgePoint { get; set; }

    public int? UploadedByUserId { get; set; }

    [ForeignKey(nameof(UploadedByUserId))]
    public AppUser? UploadedByUser { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
