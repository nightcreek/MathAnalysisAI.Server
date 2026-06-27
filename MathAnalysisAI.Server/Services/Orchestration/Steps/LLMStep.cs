using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Context;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.LLM;

namespace MathAnalysisAI.Server.Services.Orchestration.Steps;

internal sealed class LLMStep : IAnalysisPipelineStep
{
    public string StepName => "LLMStep";
    public IReadOnlyCollection<string> Reads => ["Input.PreparationResult", "Input.SemanticInput", "Runtime.AnalysisContext"];
    public IReadOnlyCollection<string> Writes => ["Runtime.ValidationError", "Runtime.NormalizedMode", "Runtime.Session", "Runtime.AnalysisContext", "Runtime.LlmResponse", "Output.Response"];

    private readonly IPersistenceService _persistenceService;
    private readonly ILLMService _llmService;
    private readonly IAnalysisContextBuilder _analysisContextBuilder;
    private readonly ILogger<LLMStep> _logger;

    public LLMStep(
        IPersistenceService persistenceService,
        ILLMService llmService,
        IAnalysisContextBuilder analysisContextBuilder,
        ILogger<LLMStep> logger)
    {
        _persistenceService = persistenceService;
        _llmService = llmService;
        _analysisContextBuilder = analysisContextBuilder;
        _logger = logger;
    }

    public async Task ExecuteAsync(AnalysisExecutionContext context, CancellationToken cancellationToken = default)
    {
        var request = context.Input.PreparationResult?.PreparedInput ?? context.Input.SemanticInput
            ?? throw new ArgumentException("Prepared analysis request is required.");
        context.Output.Response ??= AnalysisPipelineSupport.BuildFallbackResponse();

        var validationError = AnalysisPipelineSupport.ValidateRequest(request);
        if (validationError != null)
        {
            context.Runtime.ValidationError = validationError;
            context.Output.Response.StudentSolutionReview.MainIssue = validationError;
            return;
        }

        context.Runtime.NormalizedMode = string.IsNullOrWhiteSpace(request.AnalysisMode)
            ? "review_solution"
            : request.AnalysisMode.Trim();

        var session = await _persistenceService.InitializeAnalysisSessionAsync(
            request,
            context.Runtime.NormalizedMode,
            cancellationToken);

        if (session == null)
        {
            context.Output.Response.StudentSolutionReview.MainIssue = "Course not found.";
            return;
        }

        context.Runtime.Session = session;
        var course = session.Course;
        var chapter = session.Chapter;
        var problem = session.Problem;
        var studentSolution = session.StudentSolution;
        context.Runtime.AnalysisContext ??= await BuildAnalysisContextForSessionAsync(
            request,
            context.Runtime.NormalizedMode,
            course,
            chapter,
            problem,
            studentSolution,
            cancellationToken);

        context.Runtime.LlmResponse = await _llmService.ExecuteAnalysisAsync(
            request,
            course,
            chapter,
            problem,
            studentSolution,
            context.Runtime.AnalysisContext,
            session.PendingAnalysisResult.Id,
            cancellationToken);
    }

    private async Task<AnalysisContextDto> BuildAnalysisContextForSessionAsync(
        MathAnalysisAI.Server.Services.Analysis.UAO.UAOInputModel request,
        string normalizedMode,
        Course course,
        Chapter? chapter,
        Problem problem,
        StudentSolution? studentSolution,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _analysisContextBuilder.BuildAsync(
                new AnalysisContextBuildRequest
                {
                    Request = request,
                    Course = course,
                    Chapter = chapter,
                    Problem = problem,
                    StudentSolution = studentSolution,
                    NormalizedKnowledgePointCodes = null
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "AnalysisContextBuilder failed. Falling back to empty context for CourseId={CourseId}, AnalysisMode={AnalysisMode}",
                request.CourseId,
                normalizedMode);
            return AnalysisContextDto.Empty;
        }
    }
}
