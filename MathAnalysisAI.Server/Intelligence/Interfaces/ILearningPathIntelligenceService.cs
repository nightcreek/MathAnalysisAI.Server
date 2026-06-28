using MathAnalysisAI.Server.DTOs.Knowledge;

namespace MathAnalysisAI.Server.Intelligence.Interfaces;

public interface ILearningPathIntelligenceService
{
    LearningPathComputationResult Build(LearningPathIntelligenceInput input);
}

public sealed record LearningPathIntelligenceInput(
    int CourseId,
    string CourseName,
    IReadOnlyList<LearningPathKnowledgePointModel> KnowledgePoints,
    IReadOnlyList<LearningPathDependencyModel> Dependencies,
    IReadOnlyList<LearningPathUserStateModel> UserStates,
    IReadOnlyList<LearningPathMistakeModel> MistakeStats);

public sealed record LearningPathKnowledgePointModel(
    int Id,
    string Name,
    string? Code,
    string? ChapterName);

public sealed record LearningPathDependencyModel(
    int FromKnowledgePointId,
    int ToKnowledgePointId,
    string? DependencyType);

public sealed record LearningPathUserStateModel(
    int KnowledgePointId,
    int MasteryLevel,
    int PracticeCount,
    int CorrectCount);

public sealed record LearningPathMistakeModel(
    int? KnowledgePointId,
    int MistakeCount,
    int SeveritySum,
    string? MostCommonMistakeTag);

public sealed record LearningPathComputationResult(
    IReadOnlyList<LearningPathItemDto> RecommendedOrder,
    IReadOnlyList<WeakPointDto> WeakPoints,
    int MasteredCount);
