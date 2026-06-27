using MathAnalysisAI.Server.DTOs.Analysis;

namespace MathAnalysisAI.Server.Services.Knowledge;

public interface IQuestionModule
{
    Task<QuestionListResponseDto> ListAsync(
        int? courseId,
        int? chapterId,
        int? knowledgePointId,
        string? difficulty,
        string? questionType,
        string? search,
        bool publishedOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<QuestionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<QuestionDto> CreateAsync(
        CreateQuestionRequestDto request,
        int? userId,
        CancellationToken cancellationToken = default);

    Task<QuestionDto?> UpdateAsync(
        int id,
        CreateQuestionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<List<QuestionDto>> GetByKnowledgePointIdsAsync(
        List<int> knowledgePointIds,
        int maxCount,
        CancellationToken cancellationToken = default);
}
