using MathAnalysisAI.Server.DTOs.CourseMaterials;
using MathAnalysisAI.Server.Models;
using Microsoft.AspNetCore.Http;

namespace MathAnalysisAI.Server.Services.Materials
{
    public class CourseMaterialIngestionService
    {
        private const long MaxFileSizeBytes = 100L * 1024L * 1024L;
        private const int MinExtractedTextLength = 100;

        private readonly ICourseMaterialIngestionPersistenceService _persistenceService;
        private readonly CourseMaterialStorageService _storageService;
        private readonly PdfTextExtractionService _pdfTextExtractionService;
        private readonly MaterialChunkingService _materialChunkingService;
        private readonly ILogger<CourseMaterialIngestionService> _logger;

        public CourseMaterialIngestionService(
            ICourseMaterialIngestionPersistenceService persistenceService,
            CourseMaterialStorageService storageService,
            PdfTextExtractionService pdfTextExtractionService,
            MaterialChunkingService materialChunkingService,
            ILogger<CourseMaterialIngestionService> logger)
        {
            _persistenceService = persistenceService;
            _storageService = storageService;
            _pdfTextExtractionService = pdfTextExtractionService;
            _materialChunkingService = materialChunkingService;
            _logger = logger;
        }

        public async Task<CourseMaterialUploadResponseDto> IngestPdfAsync(
            int courseId,
            int? chapterId,
            string? title,
            string? materialKind,
            string? visibility,
            IFormFile file,
            int? uploadedByUserId = null,
            CancellationToken cancellationToken = default)
        {
            if (courseId <= 0)
            {
                throw new ArgumentException("courseId is required.");
            }

            if (file == null || file.Length <= 0)
            {
                throw new ArgumentException("file is required.");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                throw new ArgumentException("file size exceeds 100MB limit.");
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".pdf")
            {
                throw new ArgumentException("当前阶段仅支持 PDF 上传。");
            }

            var courseExists = await _persistenceService.CourseExistsAsync(courseId, cancellationToken);
            if (!courseExists)
            {
                throw new ArgumentException("courseId not found.");
            }

            if (chapterId.HasValue)
            {
                var chapterExists = await _persistenceService.ChapterExistsInCourseAsync(
                    chapterId.Value,
                    courseId,
                    cancellationToken);
                if (!chapterExists)
                {
                    throw new ArgumentException("chapterId not found in this course.");
                }
            }

            var stored = await _storageService.SavePdfAsync(file, cancellationToken);

            var duplicate = await _persistenceService.FindDuplicateMaterialAsync(courseId, stored.FileHash, cancellationToken);

            if (duplicate != null)
            {
                return new CourseMaterialUploadResponseDto
                {
                    MaterialId = duplicate.MaterialId,
                    Title = duplicate.Title,
                    OriginalFileName = duplicate.OriginalFileName,
                    ParseStatus = duplicate.ParseStatus,
                    ParseMessage = "duplicate file detected",
                    ChunkCount = duplicate.ChunkCount
                };
            }

            var material = new CourseMaterial
            {
                CourseId = courseId,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim(),
                MaterialKind = NormalizeMaterialKind(materialKind),
                Language = "zh-CN",
                Author = null,
                Edition = null,
                Publisher = null,
                Visibility = NormalizeVisibility(visibility),
                CopyrightNote = null,
                OriginalFileName = Path.GetFileName(file.FileName),
                FileExtension = ext,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                FileHash = stored.FileHash,
                StoragePath = stored.RelativePath,
                ParseStatus = "processing",
                ParseMessage = null,
                UploadedByUserId = uploadedByUserId,
                UploadedAt = DateTime.UtcNow,
                ParsedAt = null
            };

            var createdMaterial = await _persistenceService.CreateCourseMaterialAsync(material, cancellationToken);

            var response = new CourseMaterialUploadResponseDto
            {
                MaterialId = createdMaterial.MaterialId,
                Title = createdMaterial.Title,
                OriginalFileName = createdMaterial.OriginalFileName,
                ParseStatus = createdMaterial.ParseStatus,
                ParseMessage = createdMaterial.ParseMessage,
                ChunkCount = 0
            };

            try
            {
                var extraction = await _pdfTextExtractionService.ExtractAsync(stored.AbsolutePath, cancellationToken);
                if (extraction.TotalTextLength < MinExtractedTextLength)
                {
                    var parseMessage = "PDF text extraction returned too little text. OCR is required but not implemented in this stage.";
                    await _persistenceService.SaveParsedMaterialAsync(
                        response.MaterialId,
                        "ocr_pending",
                        parseMessage,
                        DateTime.UtcNow,
                        Array.Empty<MaterialChunk>(),
                        cancellationToken);

                    response.ParseStatus = "ocr_pending";
                    response.ParseMessage = parseMessage;
                    return response;
                }

                var chunks = _materialChunkingService.BuildChunks(
                    response.MaterialId,
                    courseId,
                    chapterId,
                    extraction.Pages);

                var successMessage = chunks.Count == 0 ? "No chunks generated from extracted text." : null;
                await _persistenceService.SaveParsedMaterialAsync(
                    response.MaterialId,
                    "success",
                    successMessage,
                    DateTime.UtcNow,
                    chunks,
                    cancellationToken);

                response.ParseStatus = "success";
                response.ParseMessage = successMessage;
                response.ChunkCount = chunks.Count;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Course material PDF ingestion failed. MaterialId={MaterialId}", response.MaterialId);
                var errorMessage = BuildShortError(ex.Message);
                await _persistenceService.SaveParsedMaterialAsync(
                    response.MaterialId,
                    "failed",
                    errorMessage,
                    DateTime.UtcNow,
                    Array.Empty<MaterialChunk>(),
                    cancellationToken);

                response.ParseStatus = "failed";
                response.ParseMessage = errorMessage;
                return response;
            }
        }

        private static string NormalizeMaterialKind(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "textbook";
            }

            var lower = value.Trim().ToLowerInvariant();
            return lower switch
            {
                "textbook" or "lecture_note" or "exercise_book" or "handout" or "user_note" or "other" => lower,
                _ => "other"
            };
        }

        private static string NormalizeVisibility(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "course_internal";
            }

            var lower = value.Trim().ToLowerInvariant();
            return lower is "private" or "course_internal" ? lower : "course_internal";
        }

        private static string BuildShortError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Material ingestion failed.";
            }

            var trimmed = message.Trim();
            return trimmed.Length <= 300 ? trimmed : trimmed[..300];
        }
    }
}
