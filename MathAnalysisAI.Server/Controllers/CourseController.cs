using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/courses")]
public class CourseController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CourseController> _logger;

    public CourseController(ApplicationDbContext db, ILogger<CourseController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var courses = await _db.Courses
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Code,
                    c.SubjectId,
                    chapterCount = c.Chapters.Count
                })
                .ToListAsync(cancellationToken);

            return Ok(courses);
        }
        catch (Exception ex) when (ApiExceptionClassifier.IsDatabaseFailure(ex))
        {
            _logger.LogWarning(ex, "Courses endpoint degraded due to database/schema issue.");
            return this.DegradedOk(Array.Empty<object>());
        }
    }

    [HttpGet("{courseId:int}/chapters")]
    public async Task<IActionResult> GetChapters(int courseId, CancellationToken cancellationToken)
    {
        try
        {
            var courseExists = await _db.Courses
                .AsNoTracking()
                .AnyAsync(c => c.Id == courseId, cancellationToken);

            if (!courseExists)
            {
                return this.ApiError(StatusCodes.Status404NotFound, "COURSE_NOT_FOUND", "Course not found.");
            }

            var chapters = await _db.Chapters
                .AsNoTracking()
                .Where(c => c.CourseId == courseId)
                .OrderBy(c => c.OrderIndex)
                .ThenBy(c => c.Id)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Code,
                    c.OrderIndex
                })
                .ToListAsync(cancellationToken);

            return Ok(chapters);
        }
        catch (Exception ex) when (ApiExceptionClassifier.IsDatabaseFailure(ex))
        {
            _logger.LogWarning(ex, "Course chapters endpoint degraded due to database/schema issue. CourseId={CourseId}", courseId);
            return this.DegradedOk(Array.Empty<object>());
        }
    }
}
