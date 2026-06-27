using MathAnalysisAI.Server.DTOs.PhotoSolutions;

namespace MathAnalysisAI.Server.Services.OCR
{
    public interface IOCRService
    {
        Task<PhotoSolutionOcrResponseDto> RecognizeAsync(
            PhotoSolutionOcrRequest request,
            CancellationToken cancellationToken = default);
    }
}
