using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.Stats;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/stats")]
[RequireAuth]
public class StatsController : ControllerBase
{
    private readonly PersonalStatsService _personalStatsService;
    private readonly IUserContext _userContext;

    public StatsController(PersonalStatsService personalStatsService, IUserContext userContext)
    {
        _personalStatsService = personalStatsService;
        _userContext = userContext;
    }

    [HttpGet("personal")]
    public async Task<IActionResult> GetPersonalStats([FromQuery] int? courseId, CancellationToken cancellationToken)
    {
        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
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
        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return Unauthorized(new { message = "Not logged in." });
        }

        var stats = await _personalStatsService.GetPersonalStatsAsync(currentUser.Id, courseId, cancellationToken);
        return Ok(stats.KnowledgeMastery);
    }
}
