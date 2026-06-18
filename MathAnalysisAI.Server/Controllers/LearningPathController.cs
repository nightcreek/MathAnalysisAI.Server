using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireAuth]
public class LearningPathController : ControllerBase
{
    private readonly LearningPathService _learningPathService;
    private readonly IUserContext _userContext;

    public LearningPathController(LearningPathService learningPathService, IUserContext userContext)
    {
        _learningPathService = learningPathService;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<ActionResult<LearningPathResponseDto>> GetLearningPath(
        [FromQuery] int courseId = 1,
        CancellationToken cancellationToken = default)
    {
        if (courseId <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid course ID.",
                errorCode = "invalid_course_id",
                isRetryable = false
            });
        }

        var userId = await _userContext.GetCurrentUserIdAsync(cancellationToken);
        if (userId == null)
        {
            return Unauthorized();
        }
        var path = await _learningPathService.BuildLearningPathAsync(courseId, userId.Value, cancellationToken);
        return Ok(path);
    }

    [HttpGet("weak-points")]
    public async Task<ActionResult<List<WeakPointDto>>> GetWeakPoints(
        [FromQuery] int courseId = 1,
        CancellationToken cancellationToken = default)
    {
        if (courseId <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid course ID.",
                errorCode = "invalid_course_id",
                isRetryable = false
            });
        }

        var userId = await _userContext.GetCurrentUserIdAsync(cancellationToken);
        if (userId == null)
        {
            return Unauthorized();
        }
        var path = await _learningPathService.BuildLearningPathAsync(courseId, userId.Value, cancellationToken);
        return Ok(path.WeakPoints);
    }
}
