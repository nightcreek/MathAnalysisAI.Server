using MathAnalysisAI.Server.Intelligence.Interfaces;

namespace MathAnalysisAI.Server.Intelligence.PIM;

public sealed class PersonalStatsIntelligenceService : IPersonalStatsIntelligenceService
{
    public PersonalStatsSummary Compute(
        IReadOnlyCollection<PersonalCourseProgressModel> courseStats,
        IReadOnlyCollection<PersonalKnowledgeMasteryModel> knowledgeStates)
    {
        var totalAttempts = courseStats.Sum(x => x.AttemptCount);
        var totalCorrect = courseStats.Sum(x => x.CorrectCount);
        var totalWrong = courseStats.Sum(x => x.WrongCount);
        var overallAccuracy = totalAttempts > 0
            ? Math.Round((decimal)totalCorrect / totalAttempts * 100, 1)
            : 0m;

        var masteredCount = knowledgeStates.Count(x => x.MasteryLevel >= 70);

        return new PersonalStatsSummary(
            totalAttempts,
            totalCorrect,
            totalWrong,
            overallAccuracy,
            courseStats.Count,
            knowledgeStates.Count,
            masteredCount);
    }
}
