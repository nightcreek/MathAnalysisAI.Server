using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.OCR;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Controllers
{
    [ApiController]
    [Route("api/photo-solutions")]
    [RequireAuth]
    public class PhotoSolutionsController : ControllerBase
    {
        private const decimal DefaultManualReviewConfidenceThreshold = 0.85m;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp"
        };

        private readonly ApplicationDbContext _db;
        private readonly IPhotoSolutionOcrProvider _ocrProvider;
        private readonly IUserContext _userContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PhotoSolutionsController> _logger;

        public PhotoSolutionsController(
            ApplicationDbContext db,
            IPhotoSolutionOcrProvider ocrProvider,
            IUserContext userContext,
            IConfiguration configuration,
            ILogger<PhotoSolutionsController> logger)
        {
            _db = db;
            _ocrProvider = ocrProvider;
            _userContext = userContext;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("ocr")]
        [EnableRateLimiting("ocr")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<ActionResult<PhotoSolutionOcrResponseDto>> Ocr(
            [FromForm] int courseId,
            [FromForm] int? chapterId,
            [FromForm] string? userHint,
            [FromForm] IFormFile? file,
            CancellationToken cancellationToken)
        {
            var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
            if (currentUser == null)
            {
                return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_NOT_LOGGED_IN", "Not logged in.");
            }

            if (courseId <= 0)
            {
                return BadRequest("courseId is required.");
            }

            if (file == null || file.Length <= 0)
            {
                return BadRequest("file is required.");
            }

            var maxImageBytes = _configuration.GetValue<int?>("PhotoSolutionOcr:MaxImageBytes") ?? (10 * 1024 * 1024);
            if (file.Length > maxImageBytes)
            {
                return BadRequest($"file size exceeds {maxImageBytes} bytes limit.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return BadRequest("Only image files are supported in this stage: jpg/jpeg/png/webp.");
            }

            if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            {
                return BadRequest("Unsupported image content type.");
            }

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, cancellationToken);
                imageBytes = ms.ToArray();
            }

            var request = new PhotoSolutionOcrRequest
            {
                CourseId = courseId,
                ChapterId = chapterId,
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType ?? "image/jpeg",
                ImageBytes = imageBytes,
                UserHint = userHint
            };

            try
            {
                var recognized = await _ocrProvider.RecognizeAsync(request, cancellationToken);
                if (!recognized.IsSuccess)
                {
                    _logger.LogWarning(
                        "Photo OCR provider returned failure. Provider={Provider} Model={Model} Attempt={Attempt} ErrorCode={ErrorCode} StatusCode={StatusCode} Message={Message}",
                        recognized.RawProvider ?? "litellm",
                        recognized.ModelName ?? string.Empty,
                        recognized.AttemptCount,
                        recognized.ErrorCode ?? "ocr_provider_failure",
                        recognized.StatusCode,
                        recognized.ErrorMessage);

                    return StatusCode(recognized.StatusCode ?? StatusCodes.Status502BadGateway, new
                    {
                        message = recognized.ErrorMessage ?? "OCR provider failed.",
                        errorCode = recognized.ErrorCode,
                        isRetryable = recognized.IsRetryable,
                        statusCode = recognized.StatusCode,
                        attemptCount = recognized.AttemptCount
                    });
                }

                var assessment = AssessReviewState(recognized);
                var record = new PhotoSolutionOcrRecord
                {
                    UserId = currentUser.Id,
                    CourseId = courseId,
                    ChapterId = chapterId,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    ContentType = file.ContentType ?? "image/jpeg",
                    FileSizeBytes = file.Length,
                    ImageHash = ComputeSha256Hex(imageBytes),
                    UploadedAt = DateTime.UtcNow,
                    OcrProvider = string.IsNullOrWhiteSpace(recognized.RawProvider) ? "litellm" : recognized.RawProvider!,
                    OcrModelName = recognized.ModelName,
                    RecognizedProblemText = recognized.ProblemText?.Trim(),
                    RecognizedStudentSolutionText = recognized.StudentSolutionText?.Trim(),
                    DetectedSectionsJson = JsonSerializer.Serialize(recognized.DetectedSections),
                    FormulasJson = JsonSerializer.Serialize(recognized.Formulas),
                    WarningsJson = JsonSerializer.Serialize(recognized.Warnings),
                    ReviewReasonsJson = JsonSerializer.Serialize(assessment.ReviewReasons),
                    Confidence = recognized.Confidence,
                    Status = assessment.NeedsManualReview
                        ? PhotoSolutionOcrRecordStatus.NeedsManualReview
                        : PhotoSolutionOcrRecordStatus.PendingReview
                };

                _db.PhotoSolutionOcrRecords.Add(record);
                await _db.SaveChangesAsync(cancellationToken);

                return Ok(BuildResponse(record, recognized, assessment, canAnalyze: false));
            }
            catch (OperationCanceledException)
            {
                return StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Photo solution OCR provider request failed.");
                return this.ApiError(StatusCodes.Status503ServiceUnavailable, "OCR_PROVIDER_UNAVAILABLE", "OCR provider request failed.", true);
            }
            catch (Exception ex) when (ApiExceptionClassifier.IsTransientDependencyFailure(ex))
            {
                _logger.LogWarning(ex, "Photo solution OCR failed due to dependency/database issue.");
                return this.ApiError(StatusCodes.Status503ServiceUnavailable, "OCR_PROVIDER_UNAVAILABLE", "Photo solution OCR is temporarily unavailable.", true);
            }
        }

        [HttpPost("ocr/{id:int}/confirm")]
        [EnableRateLimiting("ocr")]
        public async Task<ActionResult<PhotoSolutionOcrResponseDto>> Confirm(
            int id,
            [FromBody] ConfirmPhotoSolutionOcrRequestDto? request,
            CancellationToken cancellationToken)
        {
            var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
            if (currentUser == null)
            {
                return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_NOT_LOGGED_IN", "Not logged in.");
            }

            if (request == null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            var record = await _db.PhotoSolutionOcrRecords
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (record == null)
            {
                return NotFound(new { message = "OCR record not found." });
            }

            if (record.UserId != currentUser.Id)
            {
                _logger.LogWarning(
                    "OCR confirm blocked due to ownership mismatch. OcrRecordId={OcrRecordId}, RecordUserId={RecordUserId}, CurrentUserId={CurrentUserId}, Role={Role}",
                    record.Id,
                    record.UserId,
                    currentUser.Id,
                    currentUser.Role);

                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden OCR record access." });
            }

            var confirmedProblemText = NormalizeText(request.ProblemText);
            if (string.IsNullOrWhiteSpace(confirmedProblemText))
            {
                return BadRequest(new { message = "ProblemText is required." });
            }

            var confirmedStudentSolutionText = NormalizeNullableText(request.StudentSolutionText);
            var confirmedFormulas = NormalizeFormulas(request.Formulas);

            record.ConfirmedProblemText = confirmedProblemText;
            record.ConfirmedStudentSolutionText = confirmedStudentSolutionText;
            record.ConfirmedFormulasJson = JsonSerializer.Serialize(confirmedFormulas);
            record.Status = PhotoSolutionOcrRecordStatus.Confirmed;
            record.ConfirmedAt = DateTime.UtcNow;
            record.ConfirmedByUserId = currentUser.Id;

            await _db.SaveChangesAsync(cancellationToken);

            var recognized = BuildRecognizedDto(record);
            var assessment = AssessReviewState(recognized);
            return Ok(BuildResponse(record, recognized, assessment, canAnalyze: true, isConfirmedOverride: true));
        }

        private static PhotoSolutionOcrResponseDto BuildResponse(
            PhotoSolutionOcrRecord record,
            PhotoSolutionOcrResponseDto source,
            PhotoSolutionOcrReviewAssessment assessment,
            bool canAnalyze,
            bool isConfirmedOverride = false)
        {
            return new PhotoSolutionOcrResponseDto
            {
                IsSuccess = true,
                OcrRecordId = record.Id,
                ProblemText = isConfirmedOverride
                    ? (record.ConfirmedProblemText ?? source.ProblemText)
                    : source.ProblemText,
                StudentSolutionText = isConfirmedOverride
                    ? (record.ConfirmedStudentSolutionText ?? source.StudentSolutionText)
                    : source.StudentSolutionText,
                DetectedSections = source.DetectedSections,
                Formulas = isConfirmedOverride
                    ? ParseFormulas(record.ConfirmedFormulasJson)
                    : source.Formulas,
                Warnings = source.Warnings,
                ReviewReasons = assessment.ReviewReasons,
                Confidence = source.Confidence,
                Status = record.Status,
                NeedsManualReview = record.Status == PhotoSolutionOcrRecordStatus.NeedsManualReview,
                IsConfirmed = string.Equals(record.Status, PhotoSolutionOcrRecordStatus.Confirmed, StringComparison.OrdinalIgnoreCase),
                CanAnalyze = canAnalyze,
                ErrorCode = null,
                ErrorMessage = null,
                IsRetryable = false,
                StatusCode = (int)HttpStatusCode.OK,
                AttemptCount = Math.Max(1, source.AttemptCount),
                RawProvider = source.RawProvider,
                ModelName = source.ModelName
            };
        }

        private static PhotoSolutionOcrResponseDto BuildRecognizedDto(PhotoSolutionOcrRecord record)
        {
            return new PhotoSolutionOcrResponseDto
            {
                IsSuccess = true,
                OcrRecordId = record.Id,
                ProblemText = record.RecognizedProblemText ?? string.Empty,
                StudentSolutionText = record.RecognizedStudentSolutionText ?? string.Empty,
                DetectedSections = ParseDetectedSections(record.DetectedSectionsJson),
                Formulas = ParseFormulas(record.FormulasJson),
                Warnings = ParseWarnings(record.WarningsJson),
                Confidence = record.Confidence,
                Status = record.Status,
                NeedsManualReview = record.Status == PhotoSolutionOcrRecordStatus.NeedsManualReview,
                IsConfirmed = string.Equals(record.Status, PhotoSolutionOcrRecordStatus.Confirmed, StringComparison.OrdinalIgnoreCase),
                CanAnalyze = string.Equals(record.Status, PhotoSolutionOcrRecordStatus.Confirmed, StringComparison.OrdinalIgnoreCase),
                IsRetryable = false,
                AttemptCount = 1,
                StatusCode = (int)HttpStatusCode.OK,
                RawProvider = record.OcrProvider,
                ModelName = record.OcrModelName,
                ReviewReasons = ParseStrings(record.ReviewReasonsJson)
            };
        }

        private static PhotoSolutionOcrReviewAssessment AssessReviewState(PhotoSolutionOcrResponseDto response)
        {
            var reviewReasons = new List<string>();
            var confidenceThreshold = DefaultManualReviewConfidenceThreshold;

            if (!response.Confidence.HasValue)
            {
                reviewReasons.Add("confidence_missing");
            }
            else if (response.Confidence.Value < confidenceThreshold)
            {
                reviewReasons.Add($"confidence_below_threshold:{confidenceThreshold:0.##}");
            }

            if (response.Warnings.Count > 0)
            {
                reviewReasons.Add("warnings_present");
            }

            if (ContainsUnclear(response.ProblemText) || ContainsUnclear(response.StudentSolutionText))
            {
                reviewReasons.Add("contains_unclear");
            }

            if (string.IsNullOrWhiteSpace(response.ProblemText))
            {
                reviewReasons.Add("problem_text_empty");
            }
            else if (IsObviouslyTooShort(response.ProblemText))
            {
                reviewReasons.Add("problem_text_too_short");
            }

            return new PhotoSolutionOcrReviewAssessment(reviewReasons.Count > 0, reviewReasons);
        }

        private static bool ContainsUnclear(string? text)
        {
            return !string.IsNullOrWhiteSpace(text)
                && text.Contains("[unclear]", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsObviouslyTooShort(string text)
        {
            var compact = RemoveWhitespace(text);
            return compact.Length > 0 && compact.Length < 12;
        }

        private static string RemoveWhitespace(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (!char.IsWhiteSpace(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string NormalizeText(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string? NormalizeNullableText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static List<FormulaCandidateDto> NormalizeFormulas(IEnumerable<FormulaCandidateDto>? formulas)
        {
            return (formulas ?? Enumerable.Empty<FormulaCandidateDto>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Latex))
                .Select(x => new FormulaCandidateDto
                {
                    Latex = x.Latex.Trim(),
                    Context = string.IsNullOrWhiteSpace(x.Context) ? null : x.Context.Trim()
                })
                .ToList();
        }

        private static List<DetectedSectionDto> ParseDetectedSections(string? json)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json)
                    ? new List<DetectedSectionDto>()
                    : JsonSerializer.Deserialize<List<DetectedSectionDto>>(json) ?? new List<DetectedSectionDto>();
            }
            catch
            {
                return new List<DetectedSectionDto>();
            }
        }

        private static List<FormulaCandidateDto> ParseFormulas(string? json)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json)
                    ? new List<FormulaCandidateDto>()
                    : JsonSerializer.Deserialize<List<FormulaCandidateDto>>(json) ?? new List<FormulaCandidateDto>();
            }
            catch
            {
                return new List<FormulaCandidateDto>();
            }
        }

        private static List<string> ParseWarnings(string? json)
        {
            return ParseStrings(json);
        }

        private static List<string> ParseStrings(string? json)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private sealed record PhotoSolutionOcrReviewAssessment(bool NeedsManualReview, List<string> ReviewReasons);
    }
}
