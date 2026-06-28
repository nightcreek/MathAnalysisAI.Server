using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Intelligence.Interfaces;

namespace MathAnalysisAI.Server.Intelligence.ExperienceOS;

public sealed class LearningPathIntelligenceService : ILearningPathIntelligenceService
{
    private readonly IWeakPointInsightProjector _weakPointInsightProjector;

    public LearningPathIntelligenceService(IWeakPointInsightProjector weakPointInsightProjector)
    {
        _weakPointInsightProjector = weakPointInsightProjector;
    }

    public LearningPathComputationResult Build(LearningPathIntelligenceInput input)
    {
        var prerequisiteMap = new Dictionary<int, List<int>>();
        var dependentMap = new Dictionary<int, List<int>>();
        foreach (var kp in input.KnowledgePoints)
        {
            prerequisiteMap[kp.Id] = new List<int>();
            dependentMap[kp.Id] = new List<int>();
        }

        foreach (var dep in input.Dependencies)
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

        var masteryMap = input.UserStates.ToDictionary(
            s => s.KnowledgePointId,
            s => new MasterySummary
            {
                MasteryLevel = s.MasteryLevel,
                PracticeCount = s.PracticeCount,
                CorrectCount = s.CorrectCount
            });

        var mistakeMap = input.MistakeStats
            .Where(m => m.KnowledgePointId.HasValue)
            .ToDictionary(m => m.KnowledgePointId!.Value, m => m);

        var items = new List<LearningPathItemDto>();
        foreach (var kp in input.KnowledgePoints)
        {
            masteryMap.TryGetValue(kp.Id, out var state);
            mistakeMap.TryGetValue(kp.Id, out var mistakes);

            var masteryLevel = state?.MasteryLevel ?? 0;
            var mistakeCount = mistakes?.MistakeCount ?? 0;
            var severitySum = mistakes?.SeveritySum ?? 0;

            var unmetPrerequisites = prerequisiteMap[kp.Id]
                .Where(prereqId =>
                {
                    masteryMap.TryGetValue(prereqId, out var prereqState);
                    return (prereqState?.MasteryLevel ?? 0) < 50;
                })
                .ToList();

            var unmetPrereqNames = input.KnowledgePoints
                .Where(x => unmetPrerequisites.Contains(x.Id))
                .Select(x => x.Name)
                .ToList();

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
                PriorityScore = ComputePriorityScore(
                    masteryLevel,
                    mistakeCount,
                    severitySum,
                    unmetPrerequisites.Count,
                    prerequisiteMap[kp.Id].Count),
                RecommendationHint = BuildRecommendationHint(
                    masteryLevel,
                    mistakeCount,
                    unmetPrerequisites.Count)
            });
        }

        var orderedItems = items
            .OrderByDescending(i => i.PriorityScore)
            .ThenBy(i => i.UnmetPrerequisiteIds.Count)
            .ThenBy(i => i.Name)
            .ToList();

        var weakPoints = _weakPointInsightProjector.Project(orderedItems, mistakeMap);
        var masteredCount = items.Count(i => i.MasteryLevel >= 70);

        return new LearningPathComputationResult(
            orderedItems,
            weakPoints,
            masteredCount);
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
        public int MasteryLevel { get; init; }
        public int PracticeCount { get; init; }
        public int CorrectCount { get; init; }
    }
}
