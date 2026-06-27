using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.Visualization;
using MathAnalysisAI.Server.Services.Analysis.Domain;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Analysis.Application;

public static class AnalysisResultModelMapper
{
    public static AnalysisResultModel FromUao(AnalysisUao uao)
    {
        return new AnalysisResultModel
        {
            Course = uao.Course,
            Chapter = uao.Chapter,
            ProblemType = uao.ProblemType,
            Difficulty = uao.Difficulty,
            KnowledgePoints = new List<string>(uao.KnowledgePoints),
            SolutionOverview = uao.SolutionOverview,
            StandardSolution = uao.StandardSolution
                .Select(x => new AnalysisSolutionStep
                {
                    Step = x.Step,
                    Title = x.Title,
                    Content = x.Content
                })
                .ToList(),
            StudentSolutionReview = new AnalysisStudentReview
            {
                IsCorrect = uao.StudentSolutionReview.IsCorrect,
                MainIssue = uao.StudentSolutionReview.MainIssue,
                LogicGaps = new List<string>(uao.StudentSolutionReview.LogicGaps),
                Suggestions = new List<string>(uao.StudentSolutionReview.Suggestions)
            },
            MistakeTags = new List<string>(uao.MistakeTags),
            ReviewSuggestions = new List<string>(uao.ReviewSuggestions),
            Visualization = new AnalysisVisualization
            {
                ShouldUse = uao.Visualization.ShouldUse,
                Engine = uao.Visualization.Engine,
                VisualizationType = uao.Visualization.VisualizationType,
                Reason = uao.Visualization.Reason,
                GeoGebraCommands = new List<string>(uao.Visualization.GeoGebraCommands),
                Caption = uao.Visualization.Caption
            }
        };
    }

    public static AnalysisResponseDto ToResponseDto(
        AnalysisResultModel result,
        int? analysisResultId = null,
        int? problemId = null,
        int? studentSolutionId = null)
    {
        return new AnalysisResponseDto
        {
            AnalysisResultId = analysisResultId,
            ProblemId = problemId,
            StudentSolutionId = studentSolutionId,
            Course = result.Course,
            Chapter = result.Chapter,
            ProblemType = result.ProblemType,
            Difficulty = result.Difficulty,
            KnowledgePoints = new List<string>(result.KnowledgePoints),
            SolutionOverview = result.SolutionOverview,
            StandardSolution = result.StandardSolution
                .Select(x => new StandardSolutionStepDto
                {
                    Step = x.Step,
                    Title = x.Title,
                    Content = x.Content
                })
                .ToList(),
            StudentSolutionReview = new StudentSolutionReviewDto
            {
                IsCorrect = result.StudentSolutionReview.IsCorrect,
                MainIssue = result.StudentSolutionReview.MainIssue,
                LogicGaps = new List<string>(result.StudentSolutionReview.LogicGaps),
                Suggestions = new List<string>(result.StudentSolutionReview.Suggestions)
            },
            MistakeTags = new List<string>(result.MistakeTags),
            ReviewSuggestions = new List<string>(result.ReviewSuggestions),
            Visualization = new VisualizationDto
            {
                ShouldUse = result.Visualization.ShouldUse,
                Engine = result.Visualization.Engine,
                VisualizationType = result.Visualization.VisualizationType,
                Reason = result.Visualization.Reason,
                GeoGebraCommands = new List<string>(result.Visualization.GeoGebraCommands),
                Caption = result.Visualization.Caption
            },
            AnswerReliability = result.AnswerReliability,
            NeedsReview = result.NeedsReview,
            ReliabilityReasons = new List<string>(result.ReliabilityReasons),
            VerifierWarnings = new List<string>(result.VerifierWarnings),
            VerifiedAt = result.VerifiedAt
        };
    }
}
