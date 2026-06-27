using MathAnalysisAI.Server.DTOs.Stats;
using MathAnalysisAI.Server.Services.Analysis.Persistence;

namespace MathAnalysisAI.Server.Services.Stats;

public class PersonalStatsService
{
    private readonly IPersistenceService _persistenceService;

    public PersonalStatsService(IPersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
    }

    public async Task<PersonalStatsDto> GetPersonalStatsAsync(int userId, int? courseId = null, CancellationToken cancellationToken = default)
    {
        var courseStats = (await _persistenceService.GetPersonalUserCourseStatsAsync(
                new PersonalUserCourseStatsQuery(userId, courseId),
                cancellationToken))
            .Select(x => new CourseProgressDto
            {
                CourseId = x.CourseId,
                CourseName = x.Course != null ? x.Course.Name : string.Empty,
                AttemptCount = x.AttemptCount,
                CorrectCount = x.CorrectCount,
                WrongCount = x.WrongCount,
                AccuracyRate = x.AccuracyRate,
                RankingScore = x.RankingScore
            })
            .ToList();

        var knowledgeStates = (await _persistenceService.GetPersonalUserKnowledgeStatesAsync(
                new PersonalUserKnowledgeStatesQuery(userId, courseId),
                cancellationToken))
            .Select(x => new KnowledgeMasteryDto
            {
                KnowledgePointId = x.KnowledgePointId,
                KnowledgePointName = x.KnowledgePoint != null ? x.KnowledgePoint.Name : string.Empty,
                KnowledgePointCode = x.KnowledgePoint != null ? x.KnowledgePoint.Code : null,
                MasteryLevel = x.MasteryLevel,
                PracticeCount = x.PracticeCount,
                CorrectCount = x.CorrectCount
            })
            .ToList();

        var totalAttempts = courseStats.Sum(x => x.AttemptCount);
        var totalCorrect = courseStats.Sum(x => x.CorrectCount);
        var totalWrong = courseStats.Sum(x => x.WrongCount);
        var overallAccuracy = totalAttempts > 0
            ? Math.Round((decimal)totalCorrect / totalAttempts * 100, 1)
            : 0m;

        var masteredCount = knowledgeStates.Count(x => x.MasteryLevel >= 70);

        return new PersonalStatsDto
        {
            Summary = new PersonalSummaryDto
            {
                TotalAttempts = totalAttempts,
                TotalCorrect = totalCorrect,
                TotalWrong = totalWrong,
                OverallAccuracy = overallAccuracy,
                CoursesEnrolled = courseStats.Count,
                TotalKnowledgePoints = knowledgeStates.Count,
                MasteredKnowledgePoints = masteredCount
            },
            CourseProgress = courseStats,
            KnowledgeMastery = knowledgeStates
        };
    }
}
