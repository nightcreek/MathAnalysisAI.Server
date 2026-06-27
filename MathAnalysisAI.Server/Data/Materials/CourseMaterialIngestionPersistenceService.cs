using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Materials;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Data.Materials;

public sealed class CourseMaterialIngestionPersistenceService : ICourseMaterialIngestionPersistenceService
{
    private readonly ApplicationDbContext _db;

    public CourseMaterialIngestionPersistenceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken)
    {
        return await _db.Courses
            .AsNoTracking()
            .AnyAsync(x => x.Id == courseId, cancellationToken);
    }

    public async Task<bool> ChapterExistsInCourseAsync(int chapterId, int courseId, CancellationToken cancellationToken)
    {
        return await _db.Chapters
            .AsNoTracking()
            .AnyAsync(x => x.Id == chapterId && x.CourseId == courseId, cancellationToken);
    }

    public async Task<DuplicateCourseMaterialRecord?> FindDuplicateMaterialAsync(int courseId, string? fileHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
        {
            return null;
        }

        var duplicate = await _db.CourseMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.CourseId == courseId && x.FileHash != null && x.FileHash == fileHash,
                cancellationToken);

        if (duplicate == null)
        {
            return null;
        }

        return new DuplicateCourseMaterialRecord
        {
            MaterialId = duplicate.Id,
            Title = duplicate.Title,
            OriginalFileName = duplicate.OriginalFileName,
            ParseStatus = duplicate.ParseStatus,
            ChunkCount = await _db.MaterialChunks.CountAsync(x => x.CourseMaterialId == duplicate.Id, cancellationToken)
        };
    }

    public async Task<CreatedCourseMaterialRecord> CreateCourseMaterialAsync(CourseMaterial material, CancellationToken cancellationToken)
    {
        _db.CourseMaterials.Add(material);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreatedCourseMaterialRecord
        {
            MaterialId = material.Id,
            Title = material.Title,
            OriginalFileName = material.OriginalFileName,
            ParseStatus = material.ParseStatus,
            ParseMessage = material.ParseMessage
        };
    }

    public async Task SaveParsedMaterialAsync(
        int materialId,
        string parseStatus,
        string? parseMessage,
        DateTime parsedAt,
        IReadOnlyCollection<MaterialChunk> chunks,
        CancellationToken cancellationToken)
    {
        var material = await _db.CourseMaterials.FirstOrDefaultAsync(x => x.Id == materialId, cancellationToken);
        if (material == null)
        {
            throw new InvalidOperationException($"Course material {materialId} not found.");
        }

        if (chunks.Count > 0)
        {
            _db.MaterialChunks.AddRange(chunks);
        }

        material.ParseStatus = parseStatus;
        material.ParseMessage = parseMessage;
        material.ParsedAt = parsedAt;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
