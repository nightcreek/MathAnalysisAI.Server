using MathAnalysisAI.Server.DTOs.PhotoSolutions;

namespace MathAnalysisAI.Server.Services.OCR
{
    public sealed class OCRService : IOCRService
    {
        private readonly IPhotoSolutionOcrProvider _ocrProvider;
        private readonly ILogger<OCRService> _logger;

        public OCRService(
            IPhotoSolutionOcrProvider ocrProvider,
            ILogger<OCRService> logger)
        {
            _ocrProvider = ocrProvider;
            _logger = logger;
        }

        public async Task<PhotoSolutionOcrResponseDto> RecognizeAsync(
            PhotoSolutionOcrRequest request,
            CancellationToken cancellationToken = default)
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
            }

            return recognized;
        }
    }
}
