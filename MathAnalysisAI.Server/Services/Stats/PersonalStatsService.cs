using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Stats;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Services.Stats;

public class PersonalStatsService
{
    private readonly ApplicationDbContext _db;

    public PersonalStatsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PersonalStatsDto> GetPersonalStatsAsync(int userId, int? courseId = null, CancellationToken cancellationToken = default)
    {
        var courseQuery = _db.UserCourseStats
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (courseId.HasValue)
        {
            courseQuery = courseQuery.Where(x => x.CourseId == courseId.Value);
        }

        var courseStats = await courseQuery
            .OrderByDescending(x => x.AttemptCount)
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
            .ToListAsync(cancellationToken);

        var kpQuery = _db.UserKnowledgeStates
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (courseId.HasValue)
        {
            kpQuery = kpQuery.Where(x => x.KnowledgePoint.CourseId == courseId.Value);
        }

        var knowledgeStates = await kpQuery
            .OrderByDescending(x => x.MasteryLevel)
            .ThenByDescending(x => x.PracticeCount)
            .Select(x => new KnowledgeMasteryDto
            {
                KnowledgePointId = x.KnowledgePointId,
                KnowledgePointName = x.KnowledgePoint != null ? x.KnowledgePoint.Name : string.Empty,
                KnowledgePointCode = x.KnowledgePoint != null ? x.KnowledgePoint.Code : null,
                MasteryLevel = x.MasteryLevel,
                PracticeCount = x.PracticeCount,
                CorrectCount = x.CorrectCount
            })
            .ToListAsync(cancellationToken);

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
