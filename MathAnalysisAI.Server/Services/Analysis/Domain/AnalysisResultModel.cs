namespace MathAnalysisAI.Server.Services.Analysis.Domain;

public sealed class AnalysisResultModel
{
    public string Course { get; set; } = string.Empty;
    public string? Chapter { get; set; }
    public string ProblemType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<string> KnowledgePoints { get; set; } = new();
    public string SolutionOverview { get; set; } = string.Empty;
    public List<AnalysisSolutionStep> StandardSolution { get; set; } = new();
    public AnalysisStudentReview StudentSolutionReview { get; set; } = new();
    public List<string> MistakeTags { get; set; } = new();
    public List<string> ReviewSuggestions { get; set; } = new();
    public AnalysisVisualization Visualization { get; set; } = new();
    public string? AnswerReliability { get; set; }
    public bool NeedsReview { get; set; }
    public List<string> ReliabilityReasons { get; set; } = new();
    public List<string> VerifierWarnings { get; set; } = new();
    public DateTime? VerifiedAt { get; set; }
}

public sealed class AnalysisSolutionStep
{
    public int Step { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class AnalysisStudentReview
{
    public bool? IsCorrect { get; set; }
    public string? MainIssue { get; set; }
    public List<string> LogicGaps { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

public sealed class AnalysisVisualization
{
    public bool ShouldUse { get; set; }
    public string Engine { get; set; } = "none";
    public string VisualizationType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public List<string> GeoGebraCommands { get; set; } = new();
    public string? Caption { get; set; }
}
