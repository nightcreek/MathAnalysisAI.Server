using MathAnalysisAI.Server.Services.Analysis.Application;
using MathAnalysisAI.Server.Services.Analysis.Fallback;
using MathAnalysisAI.Server.Services.Analysis.Parsing;
using MathAnalysisAI.Server.Services.Analysis.Persistence;

namespace MathAnalysisAI.Server.Services.Orchestration.Steps;

internal sealed class EvaluationStep : IAnalysisPipelineStep
{
    public string StepName => "EvaluationStep";
    public IReadOnlyCollection<string> Reads => ["Input.PreparationResult", "Input.SemanticInput", "Runtime.Session", "Runtime.NormalizedMode", "Runtime.LlmResponse"];
    public IReadOnlyCollection<string> Writes => ["Runtime.ParseResult", "Runtime.SchemaError", "Output.ParsedUao", "Output.ParsedResult"];

    private readonly ILlmResponseParser _llmResponseParser;
    private readonly IAnalysisFallbackService _analysisFallbackService;
    private readonly IPersistenceService _persistenceService;

    public EvaluationStep(
        ILlmResponseParser llmResponseParser,
        IAnalysisFallbackService analysisFallbackService,
        IPersistenceService persistenceService)
    {
        _llmResponseParser = llmResponseParser;
        _analysisFallbackService = analysisFallbackService;
        _persistenceService = persistenceService;
    }

    public async Task ExecuteAsync(AnalysisExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (context.Runtime.Session == null || context.Runtime.LlmResponse == null || !context.Runtime.LlmResponse.IsSuccess)
        {
            return;
        }

        var request = context.Input.PreparationResult?.PreparedInput ?? context.Input.SemanticInput
            ?? throw new ArgumentException("Prepared analysis request is required.");

        context.Runtime.ParseResult = _llmResponseParser.Parse(context.Runtime.LlmResponse.Content);
        if (!context.Runtime.ParseResult.Success || context.Runtime.ParseResult.Parsed == null)
        {
            return;
        }

        var parsed = context.Runtime.ParseResult.Parsed ?? AnalysisPipelineSupport.BuildFallbackUao();
        AnalysisPipelineSupport.NormalizeParsedResponse(
            parsed,
            context.Runtime.Session.Course.Name,
            context.Runtime.Session.Chapter?.Name);
        _analysisFallbackService.ApplyFallbacks(
            parsed,
            context.Runtime.NormalizedMode,
            request.ProblemText,
            request.StudentSolutionText,
            context.Runtime.ParseResult.NormalizedJson ?? context.Runtime.LlmResponse.Content,
            context.Runtime.Session.Chapter?.Name);
        parsed.KnowledgePoints = await _persistenceService.NormalizeKnowledgePointsAsync(
            new NormalizeKnowledgePointsQuery(
                parsed.KnowledgePoints,
                request.CourseId,
                request.ChapterId,
                request.ProblemText,
                request.StudentSolutionText),
            cancellationToken);

        context.Runtime.SchemaError = AnalysisPipelineSupport.ValidateParsedResponse(
            parsed,
            context.Runtime.NormalizedMode,
            !string.IsNullOrWhiteSpace(request.StudentSolutionText));
        context.Output.ParsedUao = parsed;
        context.Output.ParsedResult = AnalysisResultModelMapper.FromUao(parsed);
    }
}
