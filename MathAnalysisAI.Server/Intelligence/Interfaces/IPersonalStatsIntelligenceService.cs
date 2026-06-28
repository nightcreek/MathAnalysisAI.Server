namespace MathAnalysisAI.Server.Intelligence.Interfaces;

public interface IPersonalStatsIntelligenceService
{
    PersonalStatsSummary Compute(
        IReadOnlyCollection<PersonalCourseProgressModel> courseStats,
        IReadOnlyCollection<PersonalKnowledgeMasteryModel> knowledgeStates);
}

public sealed record PersonalCourseProgressModel(
    int AttemptCount,
    int CorrectCount,
    int WrongCount);

public sealed record PersonalKnowledgeMasteryModel(
    int MasteryLevel);

public sealed record PersonalStatsSummary(
    int TotalAttempts,
    int TotalCorrect,
    int TotalWrong,
    decimal OverallAccuracy,
    int CoursesEnrolled,
    int TotalKnowledgePoints,
    int MasteredKnowledgePoints);
