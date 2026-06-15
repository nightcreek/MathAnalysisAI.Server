using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Services.Knowledge;

public class QuestionService
{
    private readonly ApplicationDbContext _db;

    public QuestionService(ApplicationDbContext db)
    {
        _db = db;
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
        var query = _db.Questions.AsNoTracking().AsQueryable();

        if (courseId.HasValue)
            query = query.Where(q => q.CourseId == courseId.Value);
        if (chapterId.HasValue)
            query = query.Where(q => q.ChapterId == chapterId.Value);
        if (knowledgePointId.HasValue)
            query = query.Where(q => q.PrimaryKnowledgePointId == knowledgePointId.Value);
        if (!string.IsNullOrWhiteSpace(difficulty))
            query = query.Where(q => q.Difficulty == difficulty);
        if (!string.IsNullOrWhiteSpace(questionType))
            query = query.Where(q => q.QuestionType == questionType);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(q => q.Title.Contains(search) || q.Content.Contains(search));
        if (publishedOnly)
            query = query.Where(q => q.IsPublished);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuestionDto
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
                ChapterName = q.Chapter != null ? q.Chapter.Name : null,
                PrimaryKnowledgePointId = q.PrimaryKnowledgePointId,
                PrimaryKnowledgePointName = q.KnowledgePoint != null ? q.KnowledgePoint.Name : null,
                IsPublished = q.IsPublished,
                CreatedAt = q.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new QuestionListResponseDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<QuestionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Questions
            .AsNoTracking()
            .Where(q => q.Id == id)
            .Select(q => new QuestionDto
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
                ChapterName = q.Chapter != null ? q.Chapter.Name : null,
                PrimaryKnowledgePointId = q.PrimaryKnowledgePointId,
                PrimaryKnowledgePointName = q.KnowledgePoint != null ? q.KnowledgePoint.Name : null,
                IsPublished = q.IsPublished,
                CreatedAt = q.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
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

        _db.Questions.Add(question);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(question.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created question could not be retrieved.");
    }

    public async Task<QuestionDto?> UpdateAsync(int id, CreateQuestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var question = await _db.Questions.FindAsync(new object[] { id }, cancellationToken);
        if (question == null)
            return null;

        question.Title = request.Title.Trim();
        question.Content = request.Content.Trim();
        question.StandardAnswer = request.StandardAnswer?.Trim();
        question.SolutionHint = request.SolutionHint?.Trim();
        question.Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "medium" : request.Difficulty;
        question.QuestionType = string.IsNullOrWhiteSpace(request.QuestionType) ? "calculation" : request.QuestionType;
        question.CourseId = request.CourseId;
        question.ChapterId = request.ChapterId;
        question.PrimaryKnowledgePointId = request.PrimaryKnowledgePointId;
        question.IsPublished = request.IsPublished;
        question.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var question = await _db.Questions.FindAsync(new object[] { id }, cancellationToken);
        if (question == null)
            return false;

        _db.Questions.Remove(question);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<QuestionDto>> GetByKnowledgePointIdsAsync(
        List<int> knowledgePointIds,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        return await _db.Questions
            .AsNoTracking()
            .Where(q => q.PrimaryKnowledgePointId != null && knowledgePointIds.Contains(q.PrimaryKnowledgePointId.Value))
            .Where(q => q.IsPublished)
            .OrderBy(q => q.Difficulty == "easy" ? 0 : q.Difficulty == "medium" ? 1 : 2)
            .Take(maxCount)
            .Select(q => new QuestionDto
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
                ChapterName = q.Chapter != null ? q.Chapter.Name : null,
                PrimaryKnowledgePointId = q.PrimaryKnowledgePointId,
                PrimaryKnowledgePointName = q.KnowledgePoint != null ? q.KnowledgePoint.Name : null,
                IsPublished = q.IsPublished,
                CreatedAt = q.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
