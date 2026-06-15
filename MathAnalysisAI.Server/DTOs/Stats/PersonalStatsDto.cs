namespace MathAnalysisAI.Server.DTOs.Stats;

public class PersonalStatsDto
{
    public PersonalSummaryDto Summary { get; set; } = new();
    public List<CourseProgressDto> CourseProgress { get; set; } = new();
    public List<KnowledgeMasteryDto> KnowledgeMastery { get; set; } = new();
}

public class PersonalSummaryDto
{
    public int TotalAttempts { get; set; }
    public int TotalCorrect { get; set; }
    public int TotalWrong { get; set; }
    public decimal OverallAccuracy { get; set; }
    public int CoursesEnrolled { get; set; }
    public int TotalKnowledgePoints { get; set; }
    public int MasteredKnowledgePoints { get; set; }
}

public class CourseProgressDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public decimal AccuracyRate { get; set; }
    public decimal RankingScore { get; set; }
}

public class KnowledgeMasteryDto
{
    public int KnowledgePointId { get; set; }
    public string KnowledgePointName { get; set; } = string.Empty;
    public string? KnowledgePointCode { get; set; }
    public int MasteryLevel { get; set; }
    public int PracticeCount { get; set; }
    public int CorrectCount { get; set; }
}
