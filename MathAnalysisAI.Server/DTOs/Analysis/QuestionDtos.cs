using System.ComponentModel.DataAnnotations;

namespace MathAnalysisAI.Server.DTOs.Analysis;

public class QuestionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? StandardAnswer { get; set; }
    public string? SolutionHint { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public int? CourseId { get; set; }
    public int? ChapterId { get; set; }
    public string? ChapterName { get; set; }
    public int? PrimaryKnowledgePointId { get; set; }
    public string? PrimaryKnowledgePointName { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuestionListResponseDto
{
    public List<QuestionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CreateQuestionRequestDto
{
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

    public int? CourseId { get; set; }
    public int? ChapterId { get; set; }
    public int? PrimaryKnowledgePointId { get; set; }
    public bool IsPublished { get; set; }
}
