using MathAnalysisAI.Server.Services.Analysis.Application;

namespace MathAnalysisAI.Server.Services.Orchestration.Steps;

internal sealed class UAOBuilderStep : IAnalysisPipelineStep
{
    public string StepName => "UAOBuilderStep";
    public IReadOnlyCollection<string> Reads => ["Input.RequestDto"];
    public IReadOnlyCollection<string> Writes => ["Input.SemanticInput"];

    public Task ExecuteAsync(AnalysisExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (context.Input.RequestDto != null)
        {
            context.Input.SemanticInput = UAOInputModelMapper.FromRequestDto(context.Input.RequestDto);
        }

        return Task.CompletedTask;
    }
}
