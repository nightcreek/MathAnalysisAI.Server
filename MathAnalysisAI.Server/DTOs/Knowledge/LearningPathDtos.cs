namespace MathAnalysisAI.Server.DTOs.Knowledge;

public class LearningPathItemDto
{
    public int KnowledgePointId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ChapterName { get; set; }
    public int MasteryLevel { get; set; }
    public int MistakeCount { get; set; }
    public List<int> UnmetPrerequisiteIds { get; set; } = new();
    public List<string> UnmetPrerequisiteNames { get; set; } = new();
    public double PriorityScore { get; set; }
    public string RecommendationHint { get; set; } = string.Empty;
    public List<QuestionRefDto> SuggestedQuestions { get; set; } = new();
}

public class QuestionRefDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
}

public class LearningPathResponseDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int TotalKnowledgePoints { get; set; }
    public int MasteredCount { get; set; }
    public List<LearningPathItemDto> RecommendedOrder { get; set; } = new();
    public List<WeakPointDto> WeakPoints { get; set; } = new();
}

public class WeakPointDto
{
    public int KnowledgePointId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ChapterName { get; set; }
    public int MistakeCount { get; set; }
    public int SeveritySum { get; set; }
    public string MostCommonMistakeTag { get; set; } = string.Empty;
    public int MasteryLevel { get; set; }
}
