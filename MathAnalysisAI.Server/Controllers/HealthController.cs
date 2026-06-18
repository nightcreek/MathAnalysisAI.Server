using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/health")]
[DisableRateLimiting]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "MathAnalysisAI.Server",
            timestampUtc = DateTime.UtcNow
        });
    }
}
