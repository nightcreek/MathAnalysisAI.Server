using MathAnalysisAI.Server.Services.Orchestration;
using MathAnalysisAI.Server.Services.Orchestration.Steps;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class AnalysisPipelineDefinitionTests
{
    [Fact]
    public void ValidateRegisteredSteps_AcceptsExpectedOrder()
    {
        var definition = new AnalysisPipelineDefinition();

        definition.ValidateRegisteredSteps(
        [
            new StubStep("UAOBuilderStep"),
            new StubStep("OCRStep"),
            new StubStep("LLMStep"),
            new StubStep("EvaluationStep"),
            new StubStep("PersistenceStep")
        ]);
    }

    [Fact]
    public void ValidateRegisteredSteps_ThrowsForDuplicateNames()
    {
        var definition = new AnalysisPipelineDefinition();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            definition.ValidateRegisteredSteps(
            [
                new StubStep("UAOBuilderStep"),
                new StubStep("OCRStep"),
                new StubStep("LLMStep"),
                new StubStep("EvaluationStep"),
                new StubStep("EvaluationStep")
            ]));

        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRegisteredSteps_ThrowsForUnexpectedOrder()
    {
        var definition = new AnalysisPipelineDefinition();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            definition.ValidateRegisteredSteps(
            [
                new StubStep("OCRStep"),
                new StubStep("UAOBuilderStep"),
                new StubStep("LLMStep"),
                new StubStep("EvaluationStep"),
                new StubStep("PersistenceStep")
            ]));

        Assert.Contains("order", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubStep(string stepName) : IAnalysisPipelineStep
    {
        public string StepName { get; } = stepName;
        public IReadOnlyCollection<string> Reads => Array.Empty<string>();
        public IReadOnlyCollection<string> Writes => Array.Empty<string>();

        public Task ExecuteAsync(AnalysisExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
