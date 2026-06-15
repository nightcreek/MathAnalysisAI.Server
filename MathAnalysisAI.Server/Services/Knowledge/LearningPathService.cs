using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Services.Knowledge;

public class LearningPathService
{
    private readonly ApplicationDbContext _db;
    private readonly QuestionService _questionService;

    public LearningPathService(ApplicationDbContext db, QuestionService questionService)
    {
        _db = db;
        _questionService = questionService;
    }

    public async Task<LearningPathResponseDto> BuildLearningPathAsync(
        int courseId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var course = await _db.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        var allKnowledgePoints = await _db.KnowledgePoints
            .AsNoTracking()
            .Where(kp => kp.CourseId == courseId)
            .Select(kp => new
            {
                kp.Id,
                kp.Name,
                kp.Code,
                ChapterName = kp.Chapter != null ? kp.Chapter.Name : null
            })
            .ToListAsync(cancellationToken);

        var dependencies = await _db.KnowledgeDependencies
            .AsNoTracking()
            .Where(d => d.FromKnowledgePoint != null && d.FromKnowledgePoint.CourseId == courseId)
            .Select(d => new
            {
                d.FromKnowledgePointId,
                d.ToKnowledgePointId,
                d.DependencyType
            })
            .ToListAsync(cancellationToken);

        var userStates = await _db.UserKnowledgeStates
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                s.KnowledgePointId,
                s.MasteryLevel,
                s.PracticeCount,
                s.CorrectCount
            })
            .ToListAsync(cancellationToken);

        var mistakeStats = await _db.MistakeRecords
            .AsNoTracking()
            .Where(m => m.KnowledgePointId != null
                        && m.AnalysisResult != null
                        && m.AnalysisResult.StudentSolution != null
                        && m.AnalysisResult.StudentSolution.UserId == userId)
            .GroupBy(m => new { m.KnowledgePointId })
            .Select(g => new
            {
                g.Key.KnowledgePointId,
                MistakeCount = g.Count(),
                SeveritySum = g.Sum(m => m.Severity),
                MostCommonMistakeTag = g
                    .GroupBy(m => m.MistakeTag)
                    .OrderByDescending(tg => tg.Count())
                    .Select(tg => tg.Key)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var prerequisiteMap = new Dictionary<int, List<int>>();
        var dependentMap = new Dictionary<int, List<int>>();
        foreach (var kp in allKnowledgePoints)
        {
            prerequisiteMap[kp.Id] = new List<int>();
            dependentMap[kp.Id] = new List<int>();
        }

        foreach (var dep in dependencies)
        {
            if (prerequisiteMap.ContainsKey(dep.ToKnowledgePointId))
            {
                prerequisiteMap[dep.ToKnowledgePointId].Add(dep.FromKnowledgePointId);
            }
            if (dependentMap.ContainsKey(dep.FromKnowledgePointId))
            {
                dependentMap[dep.FromKnowledgePointId].Add(dep.ToKnowledgePointId);
            }
        }

        var masteryMap = userStates.ToDictionary(
            s => s.KnowledgePointId,
            s => new MasterySummary
            {
                MasteryLevel = s.MasteryLevel,
                PracticeCount = s.PracticeCount,
                CorrectCount = s.CorrectCount
            });

        var mistakeMap = mistakeStats
            .Where(m => m.KnowledgePointId.HasValue)
            .ToDictionary(
                m => m.KnowledgePointId!.Value,
                m => new MistakeSummary
                {
                    MistakeCount = m.MistakeCount,
                    SeveritySum = m.SeveritySum,
                    MostCommonMistakeTag = m.MostCommonMistakeTag ?? ""
                });

        var items = new List<LearningPathItemDto>();
        foreach (var kp in allKnowledgePoints)
        {
            masteryMap.TryGetValue(kp.Id, out var state);
            mistakeMap.TryGetValue(kp.Id, out var mistakes);

            var masteryLevel = state?.MasteryLevel ?? 0;
            var mistakeCount = mistakes?.MistakeCount ?? 0;
            var severitySum = mistakes?.SeveritySum ?? 0;
            var commonMistakeTag = mistakes?.MostCommonMistakeTag ?? "";

            var unmetPrerequisites = prerequisiteMap[kp.Id]
                .Where(prereqId =>
                {
                    masteryMap.TryGetValue(prereqId, out var prereqState);
                    return (prereqState?.MasteryLevel ?? 0) < 50;
                })
                .ToList();

            var unmetPrereqNames = allKnowledgePoints
                .Where(x => unmetPrerequisites.Contains(x.Id))
                .Select(x => x.Name)
                .ToList();

            var priorityScore = ComputePriorityScore(
                masteryLevel,
                mistakeCount,
                severitySum,
                unmetPrerequisites.Count,
                prerequisiteMap[kp.Id].Count);

            var hint = BuildRecommendationHint(
                masteryLevel,
                mistakeCount,
                unmetPrerequisites.Count);

            items.Add(new LearningPathItemDto
            {
                KnowledgePointId = kp.Id,
                Name = kp.Name,
                Code = kp.Code,
                ChapterName = kp.ChapterName,
                MasteryLevel = masteryLevel,
                MistakeCount = mistakeCount,
                UnmetPrerequisiteIds = unmetPrerequisites,
                UnmetPrerequisiteNames = unmetPrereqNames,
                PriorityScore = priorityScore,
                RecommendationHint = hint
            });
        }

        var orderedItems = items
            .OrderByDescending(i => i.PriorityScore)
            .ThenBy(i => i.UnmetPrerequisiteIds.Count)
            .ThenBy(i => i.Name)
            .ToList();

        var itemsNeedingQuestions = orderedItems
            .Where(i => i.MasteryLevel < 70 || i.MistakeCount >= 2)
            .Take(10)
            .ToList();

        if (itemsNeedingQuestions.Count > 0)
        {
            var topKpIds = itemsNeedingQuestions.Select(i => i.KnowledgePointId).ToList();
            var allQuestions = await _questionService.GetByKnowledgePointIdsAsync(topKpIds, 20, cancellationToken);
            var questionsByKp = allQuestions
                .GroupBy(q => q.PrimaryKnowledgePointId ?? 0)
                .ToDictionary(g => g.Key, g => g.Take(3).ToList());

            foreach (var item in orderedItems)
            {
                if (questionsByKp.TryGetValue(item.KnowledgePointId, out var qs))
                {
                    item.SuggestedQuestions = qs
                        .Select(q => new QuestionRefDto
                        {
                            Id = q.Id,
                            Title = q.Title,
                            Difficulty = q.Difficulty
                        })
                        .ToList();
                }
            }
        }

        var weakPoints = items
            .Where(i => i.MistakeCount >= 2)
            .OrderByDescending(i => i.MistakeCount)
            .ThenByDescending(i =>
            {
                var ms = mistakeMap.GetValueOrDefault(i.KnowledgePointId);
                return i.MistakeCount > 0 && ms != null ? (double)ms.SeveritySum / i.MistakeCount : 0;
            })
            .Select(i => new WeakPointDto
            {
                KnowledgePointId = i.KnowledgePointId,
                Name = i.Name,
                Code = i.Code,
                ChapterName = i.ChapterName,
                MistakeCount = i.MistakeCount,
                SeveritySum = mistakeMap.TryGetValue(i.KnowledgePointId, out var m) ? m.SeveritySum : 0,
                MostCommonMistakeTag = mistakeMap.TryGetValue(i.KnowledgePointId, out var m2) ? m2.MostCommonMistakeTag ?? "" : "",
                MasteryLevel = i.MasteryLevel
            })
            .ToList();

        var masteredCount = items.Count(i => i.MasteryLevel >= 70);

        return new LearningPathResponseDto
        {
            CourseId = courseId,
            CourseName = course?.Name ?? "未知课程",
            TotalKnowledgePoints = allKnowledgePoints.Count,
            MasteredCount = masteredCount,
            RecommendedOrder = orderedItems,
            WeakPoints = weakPoints
        };
    }

    private static double ComputePriorityScore(
        int masteryLevel,
        int mistakeCount,
        int severitySum,
        int unmetPrereqCount,
        int totalPrereqCount)
    {
        var score = 0.0;

        score += (100 - Math.Min(masteryLevel, 100)) * 0.3;

        score += mistakeCount * 5.0;

        score += severitySum * 2.0;

        if (totalPrereqCount > 0)
        {
            var prereqRatio = (double)unmetPrereqCount / totalPrereqCount;
            score -= prereqRatio * 20.0;
        }
        else
        {
            score += 10.0;
        }

        score += 5.0;

        return Math.Max(0.0, score);
    }

    private static string BuildRecommendationHint(
        int masteryLevel,
        int mistakeCount,
        int unmetPrereqCount)
    {
        if (masteryLevel >= 90 && mistakeCount == 0)
        {
            return "已熟练掌握，可跳过复习。";
        }

        if (masteryLevel >= 70 && mistakeCount <= 1)
        {
            return "基本掌握，建议偶尔回顾。";
        }

        if (unmetPrereqCount > 0)
        {
            return $"存在 {unmetPrereqCount} 个前置知识点未掌握，建议先学习前置内容。";
        }

        if (mistakeCount >= 3)
        {
            return $"错误次数较多（{mistakeCount}次），建议重点学习。";
        }

        if (masteryLevel < 30)
        {
            return "尚未掌握，建议系统学习。";
        }

        return "建议加强练习。";
    }

    private sealed class MasterySummary
    {
        public int MasteryLevel { get; set; }
        public int PracticeCount { get; set; }
        public int CorrectCount { get; set; }
    }

    private sealed class MistakeSummary
    {
        public int MistakeCount { get; set; }
        public int SeveritySum { get; set; }
        public string MostCommonMistakeTag { get; set; } = string.Empty;
    }
}
