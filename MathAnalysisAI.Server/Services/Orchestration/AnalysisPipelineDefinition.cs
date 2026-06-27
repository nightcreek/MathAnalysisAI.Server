using MathAnalysisAI.Server.Services.Orchestration.Steps;

namespace MathAnalysisAI.Server.Services.Orchestration;

internal sealed class AnalysisPipelineDefinition
{
    public static readonly IReadOnlyList<string> OrderedSteps =
    [
        "UAOBuilderStep",
        "OCRStep",
        "LLMStep",
        "EvaluationStep",
        "PersistenceStep"
    ];

    public void ValidateRegisteredSteps(IEnumerable<IAnalysisPipelineStep> steps)
    {
        var registered = steps.Select(step => step.StepName).ToList();
        var duplicates = registered
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Analysis pipeline contains duplicate steps: {string.Join(", ", duplicates)}");
        }

        var missing = OrderedSteps
            .Where(name => !registered.Contains(name, StringComparer.Ordinal))
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Analysis pipeline is missing required steps: {string.Join(", ", missing)}");
        }

        var unexpected = registered
            .Where(name => !OrderedSteps.Contains(name, StringComparer.Ordinal))
            .ToList();
        if (unexpected.Count > 0)
        {
            throw new InvalidOperationException(
                $"Analysis pipeline contains unexpected steps: {string.Join(", ", unexpected)}");
        }

        if (!registered.SequenceEqual(OrderedSteps, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Analysis pipeline step order is invalid. Expected: {string.Join(" -> ", OrderedSteps)}. Actual: {string.Join(" -> ", registered)}");
        }
    }
}
