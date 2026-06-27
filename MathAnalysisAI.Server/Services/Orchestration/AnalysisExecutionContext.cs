using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Domain;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Analysis.Parsing;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Orchestration;

internal sealed class AnalysisExecutionContext
{
    public AnalysisInputContext Input { get; init; } = new();
    public AnalysisRuntimeContext Runtime { get; init; } = new();
    public AnalysisOutputContext Output { get; init; } = new();
}

internal sealed class AnalysisInputContext
{
    public AnalysisRequestDto? RequestDto { get; set; }
    public AppUser? CurrentUser { get; set; }
    public UAOInputModel? SemanticInput { get; set; }
    public AnalysisRequestPreparationResult? PreparationResult { get; set; }
}

internal sealed class AnalysisRuntimeContext
{
    public string? ValidationError { get; set; }
    public string NormalizedMode { get; set; } = "review_solution";
    public AnalysisPersistenceSession? Session { get; set; }
    public AnalysisContextDto? AnalysisContext { get; set; }
    public LLMChatRequestDto? LlmRequest { get; set; }
    public LLMChatResponseDto? LlmResponse { get; set; }
    public LlmParseResult? ParseResult { get; set; }
    public string? SchemaError { get; set; }
}

internal sealed class AnalysisOutputContext
{
    public AnalysisUao? ParsedUao { get; set; }
    public AnalysisResultModel? ParsedResult { get; set; }
    public AnalysisResult? AnalysisResult { get; set; }
    public AnalysisResponseDto? Response { get; set; }
}
