using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis;
using MathAnalysisAI.Server.Services.Analysis.Structuring;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/learning-analysis")]
[RequireAuth]
public class LearningAnalysisController : ControllerBase
{
    private readonly AnalysisService _analysisService;
    private readonly IProblemStructuringService _problemStructuringService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LearningAnalysisController> _logger;

    public LearningAnalysisController(
        AnalysisService analysisService,
        IProblemStructuringService problemStructuringService,
        ApplicationDbContext db,
        ILogger<LearningAnalysisController> logger)
    {
        _analysisService = analysisService;
        _problemStructuringService = problemStructuringService;
        _db = db;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [EnableRateLimiting("analyze")]
    public async Task<ActionResult<AnalysisResponseDto>> Analyze(
        [FromBody] AnalysisRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var currentUser = HttpContext.GetCurrentUser();
        if (currentUser == null)
        {
            return Unauthorized(new { message = "Not logged in." });
        }

        var originalRequestUserId = request.UserId;
        if (originalRequestUserId.HasValue && originalRequestUserId.Value != currentUser.Id)
        {
            _logger.LogWarning(
                "Analyze blocked due to userId mismatch. RequestUserId={RequestUserId}, EffectiveUserId={EffectiveUserId}, Role={Role}",
                originalRequestUserId.Value,
                currentUser.Id,
                currentUser.Role);

            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden userId mismatch." });
        }

        request.UserId = currentUser.Id;

        if (request.OcrRecordId.HasValue)
        {
            var ocrRecord = await _db.PhotoSolutionOcrRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.OcrRecordId.Value, cancellationToken);

            if (ocrRecord == null)
            {
                return NotFound(new { message = "OCR record not found." });
            }

            if (ocrRecord.UserId != currentUser.Id)
            {
                _logger.LogWarning(
                    "Analyze blocked due to OCR record ownership mismatch. OcrRecordId={OcrRecordId}, RecordUserId={RecordUserId}, CurrentUserId={CurrentUserId}, Role={Role}",
                    ocrRecord.Id,
                    ocrRecord.UserId,
                    currentUser.Id,
                    currentUser.Role);

                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden OCR record access." });
            }

            if (!string.Equals(ocrRecord.Status, PhotoSolutionOcrRecordStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    message = "OCR record is not confirmed yet.",
                    ocrRecordId = ocrRecord.Id,
                    status = ocrRecord.Status
                });
            }

            if (string.IsNullOrWhiteSpace(ocrRecord.ConfirmedProblemText))
            {
                return Conflict(new
                {
                    message = "OCR confirmation snapshot is incomplete.",
                    ocrRecordId = ocrRecord.Id
                });
            }

            if (string.IsNullOrWhiteSpace(ocrRecord.ConfirmedFormulasJson))
            {
                return Conflict(new
                {
                    message = "OCR confirmation snapshot is missing formulas.",
                    ocrRecordId = ocrRecord.Id
                });
            }

            request.CourseId = ocrRecord.CourseId;
            request.ChapterId = ocrRecord.ChapterId;
            request.ProblemText = ocrRecord.ConfirmedProblemText;
            request.StudentSolutionText = ocrRecord.ConfirmedStudentSolutionText;

            var structuredProblem = await _problemStructuringService.CreateFromConfirmedOcrAsync(
                ocrRecord,
                request,
                currentUser.Id,
                cancellationToken);

            request.StructuredProblemId = structuredProblem.Id;
            request.ProblemText = structuredProblem.NormalizedProblemText;
            request.StudentSolutionText = structuredProblem.StudentSolutionText;
            request.Formulas = ParseFormulas(structuredProblem.FormulasJson);
        }
        else
        {
            var structuredProblem = await _problemStructuringService.CreateFromManualInputAsync(
                request,
                currentUser.Id,
                cancellationToken);

            request.StructuredProblemId = structuredProblem.Id;
            request.ProblemText = structuredProblem.NormalizedProblemText;
            request.StudentSolutionText = structuredProblem.StudentSolutionText;
            request.Formulas = ParseFormulas(structuredProblem.FormulasJson);
        }

        try
        {
            var result = await _analysisService.AnalyzeAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred in learning analysis endpoint.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An internal server error occurred."
            });
        }
    }

    private static List<FormulaCandidateDto> ParseFormulas(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? new List<FormulaCandidateDto>()
                : System.Text.Json.JsonSerializer.Deserialize<List<FormulaCandidateDto>>(json) ?? new List<FormulaCandidateDto>();
        }
        catch
        {
            return new List<FormulaCandidateDto>();
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

        var currentUser = HttpContext.GetCurrentUser();
        if (currentUser == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var context = await _analysisService.BuildAnalysisContextAsync(request, cancellationToken);
        var llmRequest = await _analysisService.BuildAnalysisLlmRequestAsync(request, context, cancellationToken);

        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";
        HttpContext.Response.Headers.Connection = "keep-alive";

        await foreach (var chunk in _analysisService.StreamAnalysisAsync(llmRequest, cancellationToken))
        {
            var encoded = System.Text.Json.JsonSerializer.Serialize(chunk);
            await HttpContext.Response.WriteAsync($"data: {encoded}\n\n", cancellationToken);
            await HttpContext.Response.Body.FlushAsync(cancellationToken);
        }

        await HttpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await HttpContext.Response.Body.FlushAsync(cancellationToken);
    }
}
