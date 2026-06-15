using MathAnalysisAI.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/courses")]
public class CourseController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CourseController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
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

    [HttpGet("{courseId:int}/chapters")]
    public async Task<IActionResult> GetChapters(int courseId, CancellationToken cancellationToken)
    {
        var courseExists = await _db.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId, cancellationToken);

        if (!courseExists)
        {
            return NotFound(new { message = "Course not found." });
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
}
