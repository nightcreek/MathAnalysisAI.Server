using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Intelligence.Interfaces;

namespace MathAnalysisAI.Server.Services.Knowledge;

public class LearningPathService
{
    private readonly ILearningPathPersistenceService _learningPathPersistenceService;
    private readonly ILearningPathIntelligenceService _learningPathIntelligenceService;
    private readonly IQuestionModule _questionService;

    public LearningPathService(
        ILearningPathPersistenceService learningPathPersistenceService,
        ILearningPathIntelligenceService learningPathIntelligenceService,
        IQuestionModule questionService)
    {
        _learningPathPersistenceService = learningPathPersistenceService;
        _learningPathIntelligenceService = learningPathIntelligenceService;
        _questionService = questionService;
    }

    public async Task<LearningPathResponseDto> BuildLearningPathAsync(
        int courseId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _learningPathPersistenceService.GetLearningPathSnapshotAsync(courseId, userId, cancellationToken);
        var computed = _learningPathIntelligenceService.Build(
            new LearningPathIntelligenceInput(
                courseId,
                snapshot.CourseName ?? "未知课程",
                snapshot.KnowledgePoints
                    .Select(kp => new LearningPathKnowledgePointModel(kp.Id, kp.Name, kp.Code, kp.ChapterName))
                    .ToList(),
                snapshot.Dependencies
                    .Select(dep => new LearningPathDependencyModel(dep.FromKnowledgePointId, dep.ToKnowledgePointId, dep.DependencyType))
                    .ToList(),
                snapshot.UserStates
                    .Select(state => new LearningPathUserStateModel(state.KnowledgePointId, state.MasteryLevel, state.PracticeCount, state.CorrectCount))
                    .ToList(),
                snapshot.MistakeStats
                    .Select(m => new LearningPathMistakeModel(m.KnowledgePointId, m.MistakeCount, m.SeveritySum, m.MostCommonMistakeTag))
                    .ToList()));

        var orderedItems = computed.RecommendedOrder
            .Select(item => new LearningPathItemDto
            {
                KnowledgePointId = item.KnowledgePointId,
                Name = item.Name,
                Code = item.Code,
                ChapterName = item.ChapterName,
                MasteryLevel = item.MasteryLevel,
                MistakeCount = item.MistakeCount,
                UnmetPrerequisiteIds = item.UnmetPrerequisiteIds.ToList(),
                UnmetPrerequisiteNames = item.UnmetPrerequisiteNames.ToList(),
                PriorityScore = item.PriorityScore,
                RecommendationHint = item.RecommendationHint,
                SuggestedQuestions = item.SuggestedQuestions.ToList()
            })
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

        return new LearningPathResponseDto
        {
            CourseId = courseId,
            CourseName = snapshot.CourseName ?? "未知课程",
            TotalKnowledgePoints = snapshot.KnowledgePoints.Count,
            MasteredCount = computed.MasteredCount,
            RecommendedOrder = orderedItems,
            WeakPoints = computed.WeakPoints.ToList()
        };
    }
}
