using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Materials;

public interface ICourseMaterialIngestionPersistenceService
{
    Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken);
    Task<bool> ChapterExistsInCourseAsync(int chapterId, int courseId, CancellationToken cancellationToken);
    Task<DuplicateCourseMaterialRecord?> FindDuplicateMaterialAsync(int courseId, string? fileHash, CancellationToken cancellationToken);
    Task<CreatedCourseMaterialRecord> CreateCourseMaterialAsync(CourseMaterial material, CancellationToken cancellationToken);
    Task SaveParsedMaterialAsync(
        int materialId,
        string parseStatus,
        string? parseMessage,
        DateTime parsedAt,
        IReadOnlyCollection<MaterialChunk> chunks,
        CancellationToken cancellationToken);
}

public sealed class DuplicateCourseMaterialRecord
{
    public int MaterialId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ParseStatus { get; init; } = string.Empty;
    public int ChunkCount { get; init; }
}

public sealed class CreatedCourseMaterialRecord
{
    public int MaterialId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ParseStatus { get; init; } = string.Empty;
    public string? ParseMessage { get; init; }
}
