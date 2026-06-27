namespace MathAnalysisAI.Server.Services.Orchestration.Steps;

internal interface IAnalysisPipelineStep
{
    string StepName { get; }
    IReadOnlyCollection<string> Reads { get; }
    IReadOnlyCollection<string> Writes { get; }
    Task ExecuteAsync(AnalysisExecutionContext context, CancellationToken cancellationToken = default);
}
