using MathAnalysisAI.Server.Services.ExceptionHandling;
using MathAnalysisAI.Server.Services.Ranking;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly LeaderboardService _leaderboardService;
    private readonly ILogger<LeaderboardController> _logger;

    public LeaderboardController(LeaderboardService leaderboardService, ILogger<LeaderboardController> logger)
    {
        _leaderboardService = leaderboardService;
        _logger = logger;
    }

    [HttpGet("public")]
    public async Task<IActionResult> GetPublicLeaderboard(
        [FromQuery] int courseId = 200,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _leaderboardService.GetPublicLeaderboardAsync(courseId, take, cancellationToken);

            var result = rows.Select(x => new
            {
                rank = x.Rank,
                username = x.Username,
                attemptCount = x.AttemptCount,
                correctCount = x.CorrectCount,
                wrongCount = x.WrongCount,
                accuracyRate = x.AccuracyRate,
                rankingScore = x.RankingScore
            });

            return Ok(result);
        }
        catch (Exception ex) when (ApiExceptionClassifier.IsDatabaseFailure(ex))
        {
            _logger.LogWarning(ex, "Leaderboard endpoint degraded due to database/schema issue. CourseId={CourseId}", courseId);
            return this.DegradedOk(Array.Empty<object>());
        }
    }
}
