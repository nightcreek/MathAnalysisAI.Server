using MathAnalysisAI.Server.SharedKernel.Analysis;

namespace MathAnalysisAI.Server.Services.Analysis.UAO;

public sealed class UAOInputModel
{
    public int CourseId { get; set; }
    public int? ChapterId { get; set; }
    public string ProblemText { get; set; } = string.Empty;
    public string? StudentSolutionText { get; set; }
    public string AnalysisMode { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public int? OcrRecordId { get; set; }
    public int? StructuredProblemId { get; set; }
    public List<FormulaCandidate> Formulas { get; set; } = new();
}
