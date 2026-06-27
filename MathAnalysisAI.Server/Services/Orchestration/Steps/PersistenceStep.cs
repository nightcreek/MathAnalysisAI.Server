using MathAnalysisAI.Server.Services.Analysis.Application;
using MathAnalysisAI.Server.Services.Analysis.Domain;
using MathAnalysisAI.Server.Services.Analysis.Mistakes;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Analysis.Stats;
using MathAnalysisAI.Server.Services.Analysis.Verification;
using MathAnalysisAI.Server.Services.Visualization;

namespace MathAnalysisAI.Server.Services.Orchestration.Steps;

internal sealed class PersistenceStep : IAnalysisPipelineStep
{
    public string StepName => "PersistenceStep";
    public IReadOnlyCollection<string> Reads => ["Input.PreparationResult", "Input.SemanticInput", "Runtime.Session", "Runtime.LlmResponse", "Runtime.ParseResult", "Runtime.SchemaError", "Output.ParsedUao", "Output.ParsedResult", "Output.Response"];
    public IReadOnlyCollection<string> Writes => ["Output.AnalysisResult", "Output.Response"];

    private readonly IPersistenceService _persistenceService;
    private readonly IMistakeRecordService _mistakeRecordService;
    private readonly IUserStatsUpdateService _userStatsUpdateService;
    private readonly IVisualizationService _visualizationService;
    private readonly IAnalysisVerificationService _analysisVerificationService;

    public PersistenceStep(
        IPersistenceService persistenceService,
        IMistakeRecordService mistakeRecordService,
        IUserStatsUpdateService userStatsUpdateService,
        IVisualizationService visualizationService,
        IAnalysisVerificationService analysisVerificationService)
    {
        _persistenceService = persistenceService;
        _mistakeRecordService = mistakeRecordService;
        _userStatsUpdateService = userStatsUpdateService;
        _visualizationService = visualizationService;
        _analysisVerificationService = analysisVerificationService;
    }

    public async Task ExecuteAsync(AnalysisExecutionContext context, CancellationToken cancellationToken = default)
    {
        var request = context.Input.PreparationResult?.PreparedInput ?? context.Input.SemanticInput
            ?? throw new ArgumentException("Prepared analysis request is required.");
        var response = context.Output.Response ?? AnalysisPipelineSupport.BuildFallbackResponse();
        context.Output.Response = response;

        if (context.Runtime.Session == null)
        {
            return;
        }

        var course = context.Runtime.Session.Course;
        var chapter = context.Runtime.Session.Chapter;
        var problem = context.Runtime.Session.Problem;
        var studentSolution = context.Runtime.Session.StudentSolution;
        var pendingAnalysisResult = context.Runtime.Session.PendingAnalysisResult;

        if (context.Runtime.LlmResponse == null)
        {
            return;
        }

        if (!context.Runtime.LlmResponse.IsSuccess)
        {
            var failedResult = await _persistenceService.SaveLlmFailedAsync(
                pendingAnalysisResult,
                request,
                course,
                chapter,
                context.Runtime.LlmResponse,
                cancellationToken);

            context.Output.AnalysisResult = failedResult;
            response.AnalysisResultId = failedResult.Id;
            response.ProblemId = problem.Id;
            response.StudentSolutionId = studentSolution?.Id;
            response.Course = course.Name;
            response.Chapter = chapter?.Name;
            response.StudentSolutionReview.MainIssue = $"LLM failed: {context.Runtime.LlmResponse.ErrorCode} {context.Runtime.LlmResponse.ErrorMessage}".Trim();

            await ApplyVerificationAsync(request, problem, failedResult, null, cancellationToken);
            AnalysisPipelineSupport.ApplyReliabilityToResponse(response, failedResult);
            await _userStatsUpdateService.UpdateCourseStatsAsync(
                request.UserId,
                request.CourseId,
                _userStatsUpdateService.ResolveCorrectness(failedResult),
                cancellationToken);
            return;
        }

        if (context.Runtime.ParseResult == null || !context.Runtime.ParseResult.Success || context.Runtime.ParseResult.Parsed == null)
        {
            var parseFailResult = await _persistenceService.SaveParseFailedAsync(
                pendingAnalysisResult,
                request,
                course,
                chapter,
                context.Runtime.LlmResponse,
                context.Runtime.ParseResult?.ErrorMessage ?? "Unknown parser error.",
                cancellationToken);

            context.Output.AnalysisResult = parseFailResult;
            response.AnalysisResultId = parseFailResult.Id;
            response.ProblemId = problem.Id;
            response.StudentSolutionId = studentSolution?.Id;
            response.Course = course.Name;
            response.Chapter = chapter?.Name;
            response.StudentSolutionReview.MainIssue = $"JSON parse failed: {context.Runtime.ParseResult?.ErrorMessage}";

            await ApplyVerificationAsync(request, problem, parseFailResult, null, cancellationToken);
            AnalysisPipelineSupport.ApplyReliabilityToResponse(response, parseFailResult);
            await _userStatsUpdateService.UpdateCourseStatsAsync(
                request.UserId,
                request.CourseId,
                _userStatsUpdateService.ResolveCorrectness(parseFailResult),
                cancellationToken);
            return;
        }

        var parsedResult = context.Output.ParsedResult ?? AnalysisResultModelMapper.FromUao(
            context.Output.ParsedUao ?? AnalysisPipelineSupport.BuildFallbackUao());

        if (context.Runtime.SchemaError != null)
        {
            var schemaFailResult = await _persistenceService.SaveSchemaInvalidAsync(
                pendingAnalysisResult,
                request,
                course,
                chapter,
                context.Runtime.LlmResponse,
                parsedResult,
                context.Runtime.SchemaError,
                cancellationToken);

            context.Output.AnalysisResult = schemaFailResult;
            response.AnalysisResultId = schemaFailResult.Id;
            response.ProblemId = problem.Id;
            response.StudentSolutionId = studentSolution?.Id;
            response.Course = course.Name;
            response.Chapter = chapter?.Name;
            response.StudentSolutionReview.MainIssue = $"llm_schema_invalid: {context.Runtime.SchemaError}";

            await ApplyVerificationAsync(request, problem, schemaFailResult, parsedResult, cancellationToken);
            AnalysisPipelineSupport.ApplyReliabilityToResponse(response, schemaFailResult);
            await _userStatsUpdateService.UpdateCourseStatsAsync(
                request.UserId,
                request.CourseId,
                _userStatsUpdateService.ResolveCorrectness(schemaFailResult),
                cancellationToken);
            return;
        }

        var analysisResult = await _persistenceService.SaveSuccessAsync(
            pendingAnalysisResult,
            parsedResult,
            context.Runtime.LlmResponse,
            cancellationToken);

        context.Output.AnalysisResult = analysisResult;
        var mistakeKnowledgePointIds = await _mistakeRecordService.SaveMistakeRecordsAsync(
            analysisResult.Id,
            request.CourseId,
            context.Output.ParsedUao?.KnowledgePoints ?? new List<string>(),
            context.Output.ParsedUao?.MistakeTags ?? new List<string>(),
            cancellationToken);
        await _visualizationService.SaveVisualizationAsync(
            analysisResult.Id,
            context.Output.ParsedUao?.Visualization ?? AnalysisPipelineSupport.BuildFallbackUao().Visualization,
            cancellationToken);
        await _userStatsUpdateService.UpdateAfterAnalysisAsync(
            request.UserId,
            studentSolution?.UserId,
            request.CourseId,
            context.Output.ParsedUao?.KnowledgePoints ?? new List<string>(),
            mistakeKnowledgePointIds,
            analysisResult,
            cancellationToken);

        AnalysisPipelineSupport.ApplyReliabilityToDomainResult(parsedResult, analysisResult);
        await ApplyVerificationAsync(request, problem, analysisResult, parsedResult, cancellationToken);
        AnalysisPipelineSupport.ApplyReliabilityToDomainResult(parsedResult, analysisResult);
        context.Output.Response = AnalysisResultModelMapper.ToResponseDto(
            parsedResult,
            analysisResult.Id,
            problem.Id,
            studentSolution?.Id);
    }

    private async Task ApplyVerificationAsync(
        MathAnalysisAI.Server.Services.Analysis.UAO.UAOInputModel request,
        MathAnalysisAI.Server.Models.Problem problem,
        MathAnalysisAI.Server.Models.AnalysisResult analysisResult,
        AnalysisResultModel? parsed,
        CancellationToken cancellationToken)
    {
        var artifacts = await _persistenceService.LoadVerificationArtifactsAsync(
            request,
            problem,
            cancellationToken);

        await _analysisVerificationService.VerifyAsync(
            artifacts.StructuredProblem,
            artifacts.OcrRecord,
            analysisResult,
            parsed,
            request.ProblemText,
            request.StudentSolutionText,
            cancellationToken);
    }
}
