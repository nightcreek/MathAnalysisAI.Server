namespace MathAnalysisAI.Server.Options;

public sealed class AnalysisContextOptions
{
    public const string SectionName = "AnalysisContext";

    public bool EnableKnowledgeRetrieval { get; set; }
    public int KnowledgeTopK { get; set; } = 3;
    public int MaxKnowledgeContextChars { get; set; } = 1200;
    public int MaxChunkPreviewChars { get; set; } = 400;
    public bool EnableSymbolicEvidence { get; set; }
    public int MaxSymbolicTasks { get; set; } = 2;
    public int MaxSymbolicContextChars { get; set; } = 1000;
}
