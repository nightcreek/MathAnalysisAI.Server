using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Persistence;

namespace MathAnalysisAI.Server.Services.Knowledge;

public class QuestionService : IQuestionModule
{
    private readonly IPersistenceService _persistenceService;

    public QuestionService(IPersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
    }

    public async Task<QuestionListResponseDto> ListAsync(
        int? courseId,
        int? chapterId,
        int? knowledgePointId,
        string? difficulty,
        string? questionType,
        string? search,
        bool publishedOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _persistenceService.ListQuestionsAsync(
            new QuestionListQuery(
                courseId,
                chapterId,
                knowledgePointId,
                difficulty,
                questionType,
                search,
                publishedOnly,
                page,
                pageSize),
            cancellationToken);

        return new QuestionListResponseDto
        {
            Items = result.Items.Select(MapQuestionDto).ToList(),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<QuestionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var question = await _persistenceService.GetQuestionAsync(new QuestionByIdQuery(id), cancellationToken);
        return question == null ? null : MapQuestionDto(question);
    }

    public async Task<QuestionDto> CreateAsync(CreateQuestionRequestDto request, int? userId, CancellationToken cancellationToken = default)
    {
        var question = new Question
        {
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            StandardAnswer = request.StandardAnswer?.Trim(),
            SolutionHint = request.SolutionHint?.Trim(),
            Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "medium" : request.Difficulty,
            QuestionType = string.IsNullOrWhiteSpace(request.QuestionType) ? "calculation" : request.QuestionType,
            CourseId = request.CourseId,
            ChapterId = request.ChapterId,
            PrimaryKnowledgePointId = request.PrimaryKnowledgePointId,
            IsPublished = request.IsPublished,
            UploadedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _persistenceService.CreateQuestionAsync(new CreateQuestionCommand(question), cancellationToken);

        return await GetByIdAsync(question.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created question could not be retrieved.");
    }

    public async Task<QuestionDto?> UpdateAsync(int id, CreateQuestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var question = await _persistenceService.UpdateQuestionAsync(
            new UpdateQuestionCommand(
                id,
                request.Title.Trim(),
                request.Content.Trim(),
                request.StandardAnswer?.Trim(),
                request.SolutionHint?.Trim(),
                string.IsNullOrWhiteSpace(request.Difficulty) ? "medium" : request.Difficulty,
                string.IsNullOrWhiteSpace(request.QuestionType) ? "calculation" : request.QuestionType,
                request.CourseId,
                request.ChapterId,
                request.PrimaryKnowledgePointId,
                request.IsPublished,
                DateTime.UtcNow),
            cancellationToken);
        if (question == null)
            return null;

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _persistenceService.DeleteQuestionAsync(new DeleteQuestionCommand(id), cancellationToken);
    }

    public async Task<List<QuestionDto>> GetByKnowledgePointIdsAsync(
        List<int> knowledgePointIds,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var questions = await _persistenceService.GetPublishedQuestionsByKnowledgePointIdsAsync(
            new PublishedQuestionsByKnowledgePointsQuery(knowledgePointIds, maxCount),
            cancellationToken);
        return questions.Select(MapQuestionDto).ToList();
    }

    private static QuestionDto MapQuestionDto(Question q)
    {
        return new QuestionDto
        {
            Id = q.Id,
            Title = q.Title,
            Content = q.Content,
            StandardAnswer = q.StandardAnswer,
            SolutionHint = q.SolutionHint,
            Difficulty = q.Difficulty,
            QuestionType = q.QuestionType,
            CourseId = q.CourseId,
            ChapterId = q.ChapterId,
            ChapterName = q.Chapter?.Name,
            PrimaryKnowledgePointId = q.PrimaryKnowledgePointId,
            PrimaryKnowledgePointName = q.KnowledgePoint?.Name,
            IsPublished = q.IsPublished,
            CreatedAt = q.CreatedAt
        };
    }
}
