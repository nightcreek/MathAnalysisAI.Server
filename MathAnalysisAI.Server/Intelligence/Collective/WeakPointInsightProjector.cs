using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Intelligence.Interfaces;

namespace MathAnalysisAI.Server.Intelligence.Collective;

public sealed class WeakPointInsightProjector : IWeakPointInsightProjector
{
    public IReadOnlyList<WeakPointDto> Project(
        IReadOnlyList<LearningPathItemDto> items,
        IReadOnlyDictionary<int, LearningPathMistakeModel> mistakeMap)
    {
        return items
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
    }
}
