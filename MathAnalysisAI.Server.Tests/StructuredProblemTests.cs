using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Structuring;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class StructuredProblemTests
{
    [Fact]
    public async Task ManualInput_ShouldCreateStructuredProblem_AndKeepManualPathWorking()
    {
        var db = TestDb.Create(nameof(ManualInput_ShouldCreateStructuredProblem_AndKeepManualPathWorking));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "设 f(x)=x^2，求极限",
            StudentSolutionText = "x^2",
            AnalysisMode = "review_solution",
            UserId = user.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var structured = await db.StructuredProblems.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(StructuredProblemSourceType.Manual, structured.SourceType);
        Assert.Equal("设 f(x)=x^2，求极限", structured.RawProblemText);
        Assert.Equal("设 f(x)=x^2，求极限", structured.NormalizedProblemText);
        Assert.Equal("x^2", structured.StudentSolutionText);
        Assert.Equal(StructuredProblemStatus.Structured, structured.Status);

        var problem = await db.Problems.OrderByDescending(x => x.Id).FirstAsync();
        var analysisResult = await db.AnalysisResults.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(structured.Id, problem.StructuredProblemId);
        Assert.Equal(structured.Id, analysisResult.StructuredProblemId);
        Assert.Null(problem.PhotoSolutionOcrRecordId);
        Assert.Null(analysisResult.PhotoSolutionOcrRecordId);
    }

    [Fact]
    public async Task ManualInput_ShouldPersistMathLiveFormulasIntoStructuredProblem()
    {
        var db = TestDb.Create(nameof(ManualInput_ShouldPersistMathLiveFormulasIntoStructuredProblem));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "设 f(x)=x^2，求极限",
            StudentSolutionText = "x^2",
            AnalysisMode = "review_solution",
            UserId = user.Id,
            Formulas = new List<FormulaCandidateDto>
            {
                new() { Latex = "x^2", Context = "题目" }
            }
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var structured = await db.StructuredProblems.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(StructuredProblemSourceType.MathLive, structured.SourceType);
        Assert.Contains("\"Latex\":\"x^2\"", structured.FormulasJson);
    }

    [Fact]
    public async Task ConfirmedOcr_ShouldCreateStructuredProblem_UsingServerSnapshot()
    {
        var db = TestDb.Create(nameof(ConfirmedOcr_ShouldCreateStructuredProblem_UsingServerSnapshot));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var ocrRecord = TestServiceFactory.SeedOcrRecord(
            db,
            user,
            course,
            PhotoSolutionOcrRecordStatus.Confirmed,
            recognizedProblemText: "前端临时题目",
            recognizedStudentSolutionText: "前端临时解答",
            confirmedProblemText: "设 f(x)=x^2，求极限",
            confirmedStudentSolutionText: "x^2",
            confirmedFormulasJson: "[{\"Latex\":\"x^2\",\"Context\":\"确认\"}]");

        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id + 99,
            ProblemText = "前端传来的假题目",
            StudentSolutionText = "前端传来的假解答",
            AnalysisMode = "review_solution",
            UserId = user.Id,
            OcrRecordId = ocrRecord.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var structured = await db.StructuredProblems.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(StructuredProblemSourceType.OCR, structured.SourceType);
        Assert.Equal("前端临时题目", structured.RawProblemText);
        Assert.Equal("设 f(x)=x^2，求极限", structured.NormalizedProblemText);
        Assert.Equal("x^2", structured.StudentSolutionText);
        Assert.Contains("\"Latex\":\"x^2\"", structured.FormulasJson);

        var problem = await db.Problems.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(structured.Id, problem.StructuredProblemId);
        Assert.Equal(ocrRecord.Id, problem.PhotoSolutionOcrRecordId);
    }

    [Fact]
    public async Task UnconfirmedOcr_ShouldStillReturn409_WhenStructuredLayerExists()
    {
        var db = TestDb.Create(nameof(UnconfirmedOcr_ShouldStillReturn409_WhenStructuredLayerExists));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var ocrRecord = TestServiceFactory.SeedOcrRecord(db, user, course, PhotoSolutionOcrRecordStatus.PendingReview);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "前端题目",
            AnalysisMode = "review_solution",
            UserId = user.Id,
            OcrRecordId = ocrRecord.Id
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Empty(db.StructuredProblems);
    }
}
