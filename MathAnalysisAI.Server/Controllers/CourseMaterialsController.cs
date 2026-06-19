using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.CourseMaterials;
using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using MathAnalysisAI.Server.Services.Knowledge;
using MathAnalysisAI.Server.Services.Materials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/course-materials")]
public class CourseMaterialsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CourseMaterialIngestionService _ingestionService;
    private readonly IKnowledgeRetrievalService _knowledgeRetrievalService;
    private readonly ILogger<CourseMaterialsController> _logger;

    public CourseMaterialsController(
        ApplicationDbContext db,
        CourseMaterialIngestionService ingestionService,
        IKnowledgeRetrievalService knowledgeRetrievalService,
        ILogger<CourseMaterialsController> logger)
    {
        _db = db;
        _ingestionService = ingestionService;
        _knowledgeRetrievalService = knowledgeRetrievalService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<CourseMaterialListItemDto>>> List(
        [FromQuery] int courseId,
        [FromQuery] int? chapterId,
        [FromQuery] string? parseStatus,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        if (courseId <= 0)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "MATERIAL_COURSE_REQUIRED", "courseId is required.");
        }

        var safeTake = Math.Clamp(take ?? 50, 1, 100);

        var query = _db.CourseMaterials
            .AsNoTracking()
            .Where(x => x.CourseId == courseId);

        if (chapterId.HasValue)
        {
            var selectedChapterId = chapterId.Value;
            query = query.Where(x => _db.MaterialChunks.Any(c => c.CourseMaterialId == x.Id && c.ChapterId == selectedChapterId));
        }

        if (!string.IsNullOrWhiteSpace(parseStatus))
        {
            var normalizedStatus = parseStatus.Trim().ToLowerInvariant();
            query = query.Where(x => x.ParseStatus.ToLower() == normalizedStatus);
        }

        var items = await query
            .OrderByDescending(x => x.UploadedAt)
            .Take(safeTake)
            .Select(x => new CourseMaterialListItemDto
            {
                MaterialId = x.Id,
                CourseId = x.CourseId,
                ChapterId = _db.MaterialChunks
                    .Where(c => c.CourseMaterialId == x.Id && c.ChapterId != null)
                    .OrderBy(c => c.ChunkIndex)
                    .Select(c => c.ChapterId)
                    .FirstOrDefault(),
                Title = x.Title,
                MaterialKind = x.MaterialKind,
                OriginalFileName = x.OriginalFileName,
                FileExtension = x.FileExtension,
                FileSizeBytes = x.FileSizeBytes,
                ParseStatus = x.ParseStatus,
                ParseMessage = x.ParseMessage,
                ChunkCount = x.Chunks.Count,
                UploadedAt = x.UploadedAt,
                ParsedAt = x.ParsedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("search")]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult<IReadOnlyList<KnowledgeChunkContextDto>>> Search(
        [FromQuery] int courseId,
        [FromQuery] int? chapterId,
        [FromQuery] string? q,
        [FromQuery] string? studentSolutionText,
        [FromQuery] int? topK,
        CancellationToken cancellationToken)
    {
        if (courseId <= 0)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "MATERIAL_COURSE_REQUIRED", "courseId is required.");
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "MATERIAL_QUERY_REQUIRED", "q is required.");
        }

        var request = new KnowledgeRetrievalRequest
        {
            CourseId = courseId,
            ChapterId = chapterId,
            ProblemText = q.Trim(),
            StudentSolutionText = string.IsNullOrWhiteSpace(studentSolutionText) ? null : studentSolutionText.Trim(),
            TopK = Math.Clamp(topK ?? 3, 1, 8)
        };

        var result = await _knowledgeRetrievalService.RetrieveAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("upload")]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    public async Task<ActionResult<CourseMaterialUploadResponseDto>> Upload(
        [FromForm] int courseId,
        [FromForm] int? chapterId,
        [FromForm] string? title,
        [FromForm] string? materialKind,
        [FromForm] string? visibility,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "MATERIAL_FILE_REQUIRED", "file is required.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "MATERIAL_FILE_TYPE_UNSUPPORTED", "当前阶段仅支持 PDF。");
        }

        try
        {
            var result = await _ingestionService.IngestPdfAsync(
                courseId,
                chapterId,
                title,
                materialKind,
                visibility,
                file,
                uploadedByUserId: await HttpContext.RequestServices.GetRequiredService<IUserContext>().GetCurrentUserIdAsync(cancellationToken),
                cancellationToken: cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "MATERIAL_UPLOAD_INVALID", ex.Message);
        }
        catch (Exception ex) when (ApiExceptionClassifier.IsTransientDependencyFailure(ex))
        {
            _logger.LogWarning(ex, "Course material upload failed due to dependency/database issue.");
            return this.ApiError(StatusCodes.Status503ServiceUnavailable, "MATERIAL_UPLOAD_UNAVAILABLE", "Course material upload is temporarily unavailable.", true);
        }
    }
}
