using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Services.Stats;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/stats")]
[RequireAuth]
public class StatsController : ControllerBase
{
    private readonly PersonalStatsService _personalStatsService;

    public StatsController(PersonalStatsService personalStatsService)
    {
        _personalStatsService = personalStatsService;
    }

    [HttpGet("personal")]
    public async Task<IActionResult> GetPersonalStats([FromQuery] int? courseId, CancellationToken cancellationToken)
    {
        var currentUser = HttpContext.GetCurrentUser();
        if (currentUser == null)
        {
            return Unauthorized(new { message = "Not logged in." });
        }

        var stats = await _personalStatsService.GetPersonalStatsAsync(currentUser.Id, courseId, cancellationToken);
        return Ok(stats);
    }

    [HttpGet("knowledge-mastery")]
    public async Task<IActionResult> GetKnowledgeMastery([FromQuery] int? courseId, CancellationToken cancellationToken)
    {
        var currentUser = HttpContext.GetCurrentUser();
        if (currentUser == null)
        {
            return Unauthorized(new { message = "Not logged in." });
        }

        var stats = await _personalStatsService.GetPersonalStatsAsync(currentUser.Id, courseId, cancellationToken);
        return Ok(stats.KnowledgeMastery);
    }
}
