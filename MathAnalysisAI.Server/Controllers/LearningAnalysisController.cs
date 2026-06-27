using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Services.Analysis;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/learning-analysis")]
[RequireAuth]
public class LearningAnalysisController : ControllerBase
{
    private readonly IAnalysisService _analysisService;
    private readonly IUserContext _userContext;

    public LearningAnalysisController(
        IAnalysisService analysisService,
        IUserContext userContext)
    {
        _analysisService = analysisService;
        _userContext = userContext;
    }

    [HttpPost("analyze")]
    [EnableRateLimiting("analyze")]
    public async Task<ActionResult<AnalysisResponseDto>> Analyze(
        [FromBody] AnalysisRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "ANALYSIS_REQUEST_REQUIRED", "Request body is required.");
        }

        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_NOT_LOGGED_IN", "Not logged in.");
        }

        try
        {
            var pipelineResult = await _analysisService.AnalyzeAsync(request, currentUser, cancellationToken);
            if (!pipelineResult.Success)
            {
                return BuildPipelineFailure(pipelineResult.StatusCode, pipelineResult.Message, pipelineResult.OcrRecordId, pipelineResult.OcrStatus);
            }

            return Ok(pipelineResult.Response);
        }
        catch (ArgumentException ex)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "ANALYSIS_INVALID_REQUEST", ex.Message);
        }
    }

    [HttpPost("analyze/stream")]
    [EnableRateLimiting("analyze")]
    public async Task StreamAnalyze(
        [FromBody] AnalysisRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var streamPreparation = await _analysisService.PrepareStreamAsync(request, currentUser, cancellationToken);
        if (!streamPreparation.Success)
        {
            HttpContext.Response.StatusCode = streamPreparation.StatusCode;
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";
        HttpContext.Response.Headers.Connection = "keep-alive";

        await foreach (var chunk in streamPreparation.Stream!.WithCancellation(cancellationToken))
        {
            var encoded = System.Text.Json.JsonSerializer.Serialize(chunk);
            await HttpContext.Response.WriteAsync($"data: {encoded}\n\n", cancellationToken);
            await HttpContext.Response.Body.FlushAsync(cancellationToken);
        }

        await HttpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await HttpContext.Response.Body.FlushAsync(cancellationToken);
    }

    private ActionResult<AnalysisResponseDto> BuildPipelineFailure(
        int statusCode,
        string? message,
        int? ocrRecordId,
        string? ocrStatus)
    {
        if (statusCode == StatusCodes.Status404NotFound)
        {
            return NotFound(new { message });
        }

        if (statusCode == StatusCodes.Status403Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message });
        }

        if (statusCode == StatusCodes.Status409Conflict)
        {
            return Conflict(new
            {
                message,
                ocrRecordId,
                status = ocrStatus
            });
        }

        return StatusCode(statusCode, new { message });
    }
}
