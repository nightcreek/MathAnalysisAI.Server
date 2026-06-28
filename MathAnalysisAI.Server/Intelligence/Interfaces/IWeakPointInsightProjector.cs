using MathAnalysisAI.Server.DTOs.Knowledge;

namespace MathAnalysisAI.Server.Intelligence.Interfaces;

public interface IWeakPointInsightProjector
{
    IReadOnlyList<WeakPointDto> Project(
        IReadOnlyList<LearningPathItemDto> items,
        IReadOnlyDictionary<int, LearningPathMistakeModel> mistakeMap);
}
