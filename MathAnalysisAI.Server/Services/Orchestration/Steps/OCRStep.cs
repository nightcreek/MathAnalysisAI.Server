using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Analysis.Structuring;

namespace MathAnalysisAI.Server.Services.Orchestration.Steps;

internal sealed class OCRStep : IAnalysisPipelineStep
{
    public string StepName => "OCRStep";
    public IReadOnlyCollection<string> Reads => ["Input.CurrentUser", "Input.SemanticInput"];
    public IReadOnlyCollection<string> Writes => ["Input.SemanticInput", "Input.PreparationResult"];

    private readonly IPersistenceService _persistenceService;
    private readonly IProblemStructuringService _problemStructuringService;
    private readonly ILogger<OCRStep> _logger;

    public OCRStep(
        IPersistenceService persistenceService,
        IProblemStructuringService problemStructuringService,
        ILogger<OCRStep> logger)
    {
        _persistenceService = persistenceService;
        _problemStructuringService = problemStructuringService;
        _logger = logger;
    }

    public async Task ExecuteAsync(AnalysisExecutionContext context, CancellationToken cancellationToken = default)
    {
        var request = context.Input.SemanticInput ?? throw new ArgumentException("Semantic input is required.");
        var currentUser = context.Input.CurrentUser ?? throw new ArgumentException("Current user is required.");

        var originalRequestUserId = request.UserId;
        if (originalRequestUserId.HasValue && originalRequestUserId.Value != currentUser.Id)
        {
            _logger.LogWarning(
                "Analyze blocked due to userId mismatch. RequestUserId={RequestUserId}, EffectiveUserId={EffectiveUserId}, Role={Role}",
                originalRequestUserId.Value,
                currentUser.Id,
                currentUser.Role);

            context.Input.PreparationResult = AnalysisRequestPreparationResult.Forbidden("Forbidden userId mismatch.");
            return;
        }

        request.UserId = currentUser.Id;

        if (request.OcrRecordId.HasValue)
        {
            var ocrRecord = await _persistenceService.GetPhotoSolutionOcrRecordAsync(
                new PhotoSolutionOcrRecordByIdQuery(request.OcrRecordId.Value),
                cancellationToken);

            if (ocrRecord == null)
            {
                context.Input.PreparationResult = AnalysisRequestPreparationResult.NotFound("OCR record not found.");
                return;
            }

            if (ocrRecord.UserId != currentUser.Id)
            {
                _logger.LogWarning(
                    "Analyze blocked due to OCR record ownership mismatch. OcrRecordId={OcrRecordId}, RecordUserId={RecordUserId}, CurrentUserId={CurrentUserId}, Role={Role}",
                    ocrRecord.Id,
                    ocrRecord.UserId,
                    currentUser.Id,
                    currentUser.Role);

                context.Input.PreparationResult = AnalysisRequestPreparationResult.Forbidden("Forbidden OCR record access.");
                return;
            }

            if (!string.Equals(ocrRecord.Status, PhotoSolutionOcrRecordStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
            {
                context.Input.PreparationResult = AnalysisRequestPreparationResult.Conflict(
                    "OCR record is not confirmed yet.",
                    ocrRecord.Id,
                    ocrRecord.Status);
                return;
            }

            if (string.IsNullOrWhiteSpace(ocrRecord.ConfirmedProblemText))
            {
                context.Input.PreparationResult = AnalysisRequestPreparationResult.Conflict(
                    "OCR confirmation snapshot is incomplete.",
                    ocrRecord.Id);
                return;
            }

            if (string.IsNullOrWhiteSpace(ocrRecord.ConfirmedFormulasJson))
            {
                context.Input.PreparationResult = AnalysisRequestPreparationResult.Conflict(
                    "OCR confirmation snapshot is missing formulas.",
                    ocrRecord.Id);
                return;
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
            request.Formulas = AnalysisPipelineSupport.ParseFormulas(structuredProblem.FormulasJson);
            context.Input.PreparationResult = AnalysisRequestPreparationResult.Succeeded(request);
            return;
        }

        var manualStructuredProblem = await _problemStructuringService.CreateFromManualInputAsync(
            request,
            currentUser.Id,
            cancellationToken);

        request.StructuredProblemId = manualStructuredProblem.Id;
        request.ProblemText = manualStructuredProblem.NormalizedProblemText;
        request.StudentSolutionText = manualStructuredProblem.StudentSolutionText;
        request.Formulas = AnalysisPipelineSupport.ParseFormulas(manualStructuredProblem.FormulasJson);
        context.Input.PreparationResult = AnalysisRequestPreparationResult.Succeeded(request);
    }
}
