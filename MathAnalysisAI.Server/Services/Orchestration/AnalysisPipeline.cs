using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Context;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Orchestration.Steps;
using MathAnalysisAI.Server.Services.LLM;

namespace MathAnalysisAI.Server.Services.Orchestration;

public sealed class AnalysisPipeline : IAnalysisPipeline
{
    private readonly AnalysisPipelineDefinition _pipelineDefinition;
    private readonly IPersistenceService _persistenceService;
    private readonly IAnalysisContextBuilder _analysisContextBuilder;
    private readonly ILLMService _llmService;
    private readonly IReadOnlyList<IAnalysisPipelineStep> _orderedSteps;

    internal AnalysisPipeline(
        AnalysisPipelineDefinition pipelineDefinition,
        IPersistenceService persistenceService,
        IAnalysisContextBuilder analysisContextBuilder,
        ILLMService llmService,
        UAOBuilderStep uaoBuilderStep,
        OCRStep ocrStep,
        LLMStep llmStep,
        EvaluationStep evaluationStep,
        PersistenceStep persistenceStep)
    {
        _pipelineDefinition = pipelineDefinition;
        _persistenceService = persistenceService;
        _analysisContextBuilder = analysisContextBuilder;
        _llmService = llmService;
        _orderedSteps = [uaoBuilderStep, ocrStep, llmStep, evaluationStep, persistenceStep];
        _pipelineDefinition.ValidateRegisteredSteps(_orderedSteps);
    }

    public async Task<AnalysisPipelineResult> AnalyzeAsync(
        AnalysisRequestDto request,
        AppUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, currentUser, cancellationToken);
        if (!prepared.Success || prepared.PreparedInput == null)
        {
            return AnalysisPipelineResult.Failed(
                prepared.StatusCode,
                prepared.Message,
                prepared.OcrRecordId,
                prepared.OcrStatus);
        }

        var context = new AnalysisExecutionContext
        {
            Input =
            {
                CurrentUser = currentUser,
                SemanticInput = prepared.PreparedInput,
                PreparationResult = prepared
            },
            Output =
            {
                Response = AnalysisPipelineSupport.BuildFallbackResponse()
            }
        };
        foreach (var step in _orderedSteps.Skip(2))
        {
            await step.ExecuteAsync(context, cancellationToken);
        }
        var response = context.Output.Response ?? AnalysisPipelineSupport.BuildFallbackResponse();

        return AnalysisPipelineResult.Succeeded(response);
    }

    public async Task<AnalysisStreamPipelineResult> PrepareStreamAsync(
        AnalysisRequestDto request,
        AppUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, currentUser, cancellationToken);
        if (!prepared.Success || prepared.PreparedInput == null)
        {
            return AnalysisStreamPipelineResult.Failed(
                prepared.StatusCode,
                prepared.Message,
                prepared.OcrRecordId,
                prepared.OcrStatus);
        }

        var context = await BuildAnalysisContextAsync(
            prepared.PreparedInput,
            cancellationToken);
        var llmRequest = await BuildAnalysisLlmRequestAsync(
            prepared.PreparedInput,
            context,
            cancellationToken);

        return AnalysisStreamPipelineResult.Succeeded(
            _llmService.StreamAnalysisAsync(llmRequest, cancellationToken));
    }

    private async Task<AnalysisRequestPreparationResult> PrepareAsync(
        AnalysisRequestDto request,
        AppUser currentUser,
        CancellationToken cancellationToken)
    {
        var context = new AnalysisExecutionContext
        {
            Input =
            {
                RequestDto = request,
                CurrentUser = currentUser
            }
        };
        foreach (var step in _orderedSteps.Take(2))
        {
            await step.ExecuteAsync(context, cancellationToken);
        }

        return context.Input.PreparationResult
            ?? throw new ArgumentException("Failed to prepare analysis request.");
    }

    private async Task<AnalysisContextDto> BuildAnalysisContextAsync(
        MathAnalysisAI.Server.Services.Analysis.UAO.UAOInputModel request,
        CancellationToken cancellationToken)
    {
        var course = await _persistenceService.GetCourseAsync(new CourseByIdQuery(request.CourseId), cancellationToken)
            ?? throw new ArgumentException("Course not found.");

        Chapter? chapter = null;
        if (request.ChapterId.HasValue)
        {
            chapter = await _persistenceService.GetChapterAsync(
                new ChapterByCourseQuery(request.CourseId, request.ChapterId.Value),
                cancellationToken);
        }

        var problem = new Problem
        {
            Id = 0,
            CourseId = request.CourseId,
            ChapterId = request.ChapterId,
            ContentMarkdown = request.ProblemText ?? string.Empty,
            ProblemType = "mixed",
            SourceType = "stream"
        };

        return await _analysisContextBuilder.BuildAsync(
            new AnalysisContextBuildRequest
            {
                Request = request,
                Course = course,
                Chapter = chapter,
                Problem = problem
            },
            cancellationToken);
    }

    private async Task<LLMChatRequestDto> BuildAnalysisLlmRequestAsync(
        MathAnalysisAI.Server.Services.Analysis.UAO.UAOInputModel request,
        AnalysisContextDto context,
        CancellationToken cancellationToken)
    {
        var course = await _persistenceService.GetCourseAsync(new CourseByIdQuery(request.CourseId), cancellationToken)
            ?? throw new ArgumentException("Course not found.");

        Chapter? chapter = null;
        if (request.ChapterId.HasValue)
        {
            chapter = await _persistenceService.GetChapterAsync(
                new ChapterByCourseQuery(request.CourseId, request.ChapterId.Value),
                cancellationToken);
        }

        var problem = new Problem
        {
            Id = 0,
            CourseId = request.CourseId,
            ChapterId = request.ChapterId,
            ContentMarkdown = request.ProblemText ?? string.Empty,
            ProblemType = "mixed",
            SourceType = "stream"
        };

        return await _llmService.BuildAnalysisRequestAsync(
            request,
            course,
            chapter,
            problem,
            null,
            context,
            0,
            cancellationToken);
    }
}
