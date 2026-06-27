using MathAnalysisAI.Server.SharedKernel.Analysis;
using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.Visualization;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Application;
using MathAnalysisAI.Server.Services.Analysis.Domain;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Orchestration;

internal static class AnalysisPipelineSupport
{
    public static List<FormulaCandidate> ParseFormulas(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? new List<FormulaCandidate>()
                : System.Text.Json.JsonSerializer.Deserialize<List<FormulaCandidate>>(json) ?? new List<FormulaCandidate>();
        }
        catch
        {
            return new List<FormulaCandidate>();
        }
    }

    public static string? ValidateRequest(UAOInputModel request)
    {
        if (request.CourseId <= 0)
        {
            return "CourseId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ProblemText))
        {
            return "ProblemText is required.";
        }

        if (string.IsNullOrWhiteSpace(request.AnalysisMode))
        {
            request.AnalysisMode = "review_solution";
        }

        return null;
    }

    public static void NormalizeParsedResponse(AnalysisUao response, string courseName, string? chapterName)
    {
        response.Course = string.IsNullOrWhiteSpace(response.Course) ? courseName : response.Course;
        response.Chapter ??= chapterName;
        response.ProblemType = string.IsNullOrWhiteSpace(response.ProblemType) ? "unknown" : response.ProblemType;
        response.Difficulty = string.IsNullOrWhiteSpace(response.Difficulty) ? "unknown" : response.Difficulty;
        response.KnowledgePoints ??= new List<string>();
        response.StandardSolution ??= new List<StandardSolutionStep>();
        response.StudentSolutionReview ??= new StudentSolutionReview();
        response.MistakeTags ??= new List<string>();
        response.ReviewSuggestions ??= new List<string>();
        response.Visualization ??= new VisualizationSpec
        {
            ShouldUse = false,
            Engine = "none",
            VisualizationType = "none",
            GeoGebraCommands = new List<string>()
        };
        response.SolutionOverview ??= string.Empty;
    }

    public static string? ValidateParsedResponse(
        AnalysisUao parsed,
        string analysisMode,
        bool hasStudentSolution)
    {
        if (string.IsNullOrWhiteSpace(parsed.Course))
        {
            return "course is empty";
        }

        if (string.IsNullOrWhiteSpace(parsed.ProblemType) || parsed.ProblemType.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "problemType is empty or unknown";
        }

        var hasOverview = !string.IsNullOrWhiteSpace(parsed.SolutionOverview);
        var hasStandard = parsed.StandardSolution != null && parsed.StandardSolution.Count > 0;
        if (!hasOverview && !hasStandard)
        {
            return "solutionOverview and standardSolution are both empty";
        }

        if (parsed.StudentSolutionReview == null)
        {
            return "studentSolutionReview is empty";
        }

        if (analysisMode == "review_solution" && hasStudentSolution && parsed.StudentSolutionReview.IsCorrect == null)
        {
            var explicitUnknown = IsExplicitlyUnableToJudge(parsed.StudentSolutionReview.MainIssue)
                || IsExplicitlyUnableToJudge(parsed.SolutionOverview);

            if (!explicitUnknown)
            {
                return "studentSolutionReview.isCorrect is null";
            }
        }

        return null;
    }

    public static AnalysisResponseDto BuildFallbackResponse()
    {
        return new AnalysisResponseDto
        {
            Course = string.Empty,
            Chapter = null,
            ProblemType = "unknown",
            Difficulty = "unknown",
            KnowledgePoints = new List<string>(),
            SolutionOverview = string.Empty,
            StandardSolution = new List<StandardSolutionStepDto>(),
            StudentSolutionReview = new StudentSolutionReviewDto
            {
                IsCorrect = null,
                MainIssue = null,
                LogicGaps = new List<string>(),
                Suggestions = new List<string>()
            },
            MistakeTags = new List<string>(),
            ReviewSuggestions = new List<string>(),
            Visualization = new VisualizationDto
            {
                ShouldUse = false,
                Engine = "none",
                VisualizationType = "none",
                GeoGebraCommands = new List<string>()
            }
        };
    }

    public static AnalysisUao BuildFallbackUao()
    {
        return new AnalysisUao
        {
            Course = string.Empty,
            Chapter = null,
            ProblemType = "unknown",
            Difficulty = "unknown",
            KnowledgePoints = new List<string>(),
            SolutionOverview = string.Empty,
            StandardSolution = new List<StandardSolutionStep>(),
            StudentSolutionReview = new StudentSolutionReview
            {
                IsCorrect = null,
                MainIssue = null,
                LogicGaps = new List<string>(),
                Suggestions = new List<string>()
            },
            MistakeTags = new List<string>(),
            ReviewSuggestions = new List<string>(),
            Visualization = new VisualizationSpec
            {
                ShouldUse = false,
                Engine = "none",
                VisualizationType = "none",
                GeoGebraCommands = new List<string>()
            }
        };
    }

    public static void ApplyReliabilityToResponse(AnalysisResponseDto response, AnalysisResult analysisResult)
    {
        response.AnswerReliability = analysisResult.AnswerReliability.ToString();
        response.NeedsReview = analysisResult.NeedsReview;
        response.ReliabilityReasons = ParseStrings(analysisResult.ReliabilityReasonsJson);
        response.VerifierWarnings = ParseStrings(analysisResult.VerifierWarningsJson);
        response.VerifiedAt = analysisResult.VerifiedAt;
    }

    public static void ApplyReliabilityToDomainResult(AnalysisResultModel result, AnalysisResult analysisResult)
    {
        result.AnswerReliability = analysisResult.AnswerReliability.ToString();
        result.NeedsReview = analysisResult.NeedsReview;
        result.ReliabilityReasons = ParseStrings(analysisResult.ReliabilityReasonsJson);
        result.VerifierWarnings = ParseStrings(analysisResult.VerifierWarningsJson);
        result.VerifiedAt = analysisResult.VerifiedAt;
    }

    public static List<string> ParseStrings(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static bool IsExplicitlyUnableToJudge(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.ToLowerInvariant();
        return value.Contains("无法判断")
            || value.Contains("不能判断")
            || value.Contains("insufficient")
            || value.Contains("cannot determine")
            || value.Contains("unable to determine");
    }
}
