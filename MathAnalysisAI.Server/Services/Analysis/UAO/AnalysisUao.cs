using MathAnalysisAI.Server.SharedKernel.Analysis;

namespace MathAnalysisAI.Server.Services.Analysis.UAO;

public sealed class AnalysisUao
{
    public string Course { get; set; } = string.Empty;
    public string? Chapter { get; set; }
    public string ProblemType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<string> KnowledgePoints { get; set; } = new();
    public string SolutionOverview { get; set; } = string.Empty;
    public List<StandardSolutionStep> StandardSolution { get; set; } = new();
    public StudentSolutionReview StudentSolutionReview { get; set; } = new();
    public List<string> MistakeTags { get; set; } = new();
    public List<string> ReviewSuggestions { get; set; } = new();
    public VisualizationSpec Visualization { get; set; } = new();
}
