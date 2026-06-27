using MathAnalysisAI.Server.Services.Courses;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/courses")]
public class CourseController : ControllerBase
{
    private readonly CourseService _courseService;
    private readonly ILogger<CourseController> _logger;

    public CourseController(CourseService courseService, ILogger<CourseController> logger)
    {
        _courseService = courseService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var courses = await _courseService.GetAllAsync(cancellationToken);
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
            var chapters = await _courseService.GetChaptersAsync(courseId, cancellationToken);
            if (chapters == null)
            {
                return this.ApiError(StatusCodes.Status404NotFound, "COURSE_NOT_FOUND", "Course not found.");
            }

            return Ok(chapters);
        }
        catch (Exception ex) when (ApiExceptionClassifier.IsDatabaseFailure(ex))
        {
            _logger.LogWarning(ex, "Course chapters endpoint degraded due to database/schema issue. CourseId={CourseId}", courseId);
            return this.DegradedOk(Array.Empty<object>());
        }
    }
}
