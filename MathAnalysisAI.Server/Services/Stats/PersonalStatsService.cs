using MathAnalysisAI.Server.DTOs.Stats;
using MathAnalysisAI.Server.Intelligence.Interfaces;
using MathAnalysisAI.Server.Services.Analysis.Persistence;

namespace MathAnalysisAI.Server.Services.Stats;

public class PersonalStatsService
{
    private readonly IPersistenceService _persistenceService;
    private readonly IPersonalStatsIntelligenceService _personalStatsIntelligenceService;

    public PersonalStatsService(
        IPersistenceService persistenceService,
        IPersonalStatsIntelligenceService personalStatsIntelligenceService)
    {
        _persistenceService = persistenceService;
        _personalStatsIntelligenceService = personalStatsIntelligenceService;
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

        var summary = _personalStatsIntelligenceService.Compute(
            courseStats
                .Select(x => new PersonalCourseProgressModel(x.AttemptCount, x.CorrectCount, x.WrongCount))
                .ToList(),
            knowledgeStates
                .Select(x => new PersonalKnowledgeMasteryModel(x.MasteryLevel))
                .ToList());

        return new PersonalStatsDto
        {
            Summary = new PersonalSummaryDto
            {
                TotalAttempts = summary.TotalAttempts,
                TotalCorrect = summary.TotalCorrect,
                TotalWrong = summary.TotalWrong,
                OverallAccuracy = summary.OverallAccuracy,
                CoursesEnrolled = summary.CoursesEnrolled,
                TotalKnowledgePoints = summary.TotalKnowledgePoints,
                MasteredKnowledgePoints = summary.MasteredKnowledgePoints
            },
            CourseProgress = courseStats,
            KnowledgeMastery = knowledgeStates
        };
    }
}
