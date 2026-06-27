using MathAnalysisAI.Server.Services.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Data.Knowledge;

public sealed class LearningPathPersistenceService : ILearningPathPersistenceService
{
    private readonly ApplicationDbContext _db;

    public LearningPathPersistenceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<LearningPathSnapshot> GetLearningPathSnapshotAsync(int courseId, int userId, CancellationToken cancellationToken)
    {
        var courseName = await _db.Courses
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var knowledgePoints = await _db.KnowledgePoints
            .AsNoTracking()
            .Where(kp => kp.CourseId == courseId)
            .Select(kp => new LearningPathKnowledgePointRecord
            {
                Id = kp.Id,
                Name = kp.Name,
                Code = kp.Code,
                ChapterName = kp.Chapter != null ? kp.Chapter.Name : null
            })
            .ToListAsync(cancellationToken);

        var dependencies = await _db.KnowledgeDependencies
            .AsNoTracking()
            .Where(d => d.FromKnowledgePoint != null && d.FromKnowledgePoint.CourseId == courseId)
            .Select(d => new LearningPathDependencyRecord
            {
                FromKnowledgePointId = d.FromKnowledgePointId,
                ToKnowledgePointId = d.ToKnowledgePointId,
                DependencyType = d.DependencyType
            })
            .ToListAsync(cancellationToken);

        var userStates = await _db.UserKnowledgeStates
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new LearningPathUserStateRecord
            {
                KnowledgePointId = s.KnowledgePointId,
                MasteryLevel = s.MasteryLevel,
                PracticeCount = s.PracticeCount,
                CorrectCount = s.CorrectCount
            })
            .ToListAsync(cancellationToken);

        var mistakeStats = await _db.MistakeRecords
            .AsNoTracking()
            .Where(m => m.KnowledgePointId != null
                        && m.AnalysisResult != null
                        && m.AnalysisResult.StudentSolution != null
                        && m.AnalysisResult.StudentSolution.UserId == userId)
            .GroupBy(m => new { m.KnowledgePointId })
            .Select(g => new LearningPathMistakeRecord
            {
                KnowledgePointId = g.Key.KnowledgePointId,
                MistakeCount = g.Count(),
                SeveritySum = g.Sum(m => m.Severity),
                MostCommonMistakeTag = g
                    .GroupBy(m => m.MistakeTag)
                    .OrderByDescending(tg => tg.Count())
                    .Select(tg => tg.Key)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new LearningPathSnapshot
        {
            CourseName = courseName,
            KnowledgePoints = knowledgePoints,
            Dependencies = dependencies,
            UserStates = userStates,
            MistakeStats = mistakeStats
        };
    }
}
