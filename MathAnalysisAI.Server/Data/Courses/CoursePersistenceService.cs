using MathAnalysisAI.Server.Services.Courses;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Data.Courses;

public sealed class CoursePersistenceService : ICoursePersistenceService
{
    private readonly ApplicationDbContext _db;

    public CoursePersistenceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<CourseListItemDto>> ListCoursesAsync(CancellationToken cancellationToken)
    {
        return await _db.Courses
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CourseListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                SubjectId = c.SubjectId,
                ChapterCount = c.Chapters.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken)
    {
        return await _db.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId, cancellationToken);
    }

    public async Task<List<ChapterListItemDto>> ListCourseChaptersAsync(int courseId, CancellationToken cancellationToken)
    {
        return await _db.Chapters
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .OrderBy(c => c.OrderIndex)
            .ThenBy(c => c.Id)
            .Select(c => new ChapterListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                OrderIndex = c.OrderIndex
            })
            .ToListAsync(cancellationToken);
    }
}
