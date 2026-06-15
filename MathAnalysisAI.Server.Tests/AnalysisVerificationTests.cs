using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class AnalysisVerificationTests
{
    [Fact]
    public async Task StructuredProblemNeedsReview_ShouldMarkAnalysisNeedsReview()
    {
        var db = TestDb.Create(nameof(StructuredProblemNeedsReview_ShouldMarkAnalysisNeedsReview));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user, new FakeSuccessfulLlmResponseParser());

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "x",
            StudentSolutionText = "x",
            AnalysisMode = "review_solution",
            UserId = user.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var analysisResult = await db.AnalysisResults.OrderByDescending(x => x.Id).FirstAsync();
        Assert.True(analysisResult.NeedsReview);
        Assert.NotEqual(AnswerReliability.Reliable, analysisResult.AnswerReliability);
    }

    [Fact]
    public async Task ProblemContainingUnclear_ShouldBeUnsafeToUse()
    {
        var db = TestDb.Create(nameof(ProblemContainingUnclear_ShouldBeUnsafeToUse));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user, new FakeSuccessfulLlmResponseParser());

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "设 f(x) = x^2，求[unclear]在 x→0 时的极限",
            StudentSolutionText = "x^2",
            AnalysisMode = "review_solution",
            UserId = user.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var analysisResult = await db.AnalysisResults.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(AnswerReliability.UnsafeToUse, analysisResult.AnswerReliability);
        Assert.True(analysisResult.NeedsReview);
    }

    [Fact]
    public async Task EmptyStandardSolution_ShouldBeUnsafeToUse()
    {
        var db = TestDb.Create(nameof(EmptyStandardSolution_ShouldBeUnsafeToUse));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var parser = new FakeSuccessfulLlmResponseParser { IncludeStandardSolution = false };
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user, parser);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "已知函数 f(x)=x^2+1，求当 x→0 时的极限",
            StudentSolutionText = "1",
            AnalysisMode = "review_solution",
            UserId = user.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var analysisResult = await db.AnalysisResults.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(AnswerReliability.UnsafeToUse, analysisResult.AnswerReliability);
    }

    [Fact]
    public async Task OcrWarnings_ShouldAtLeastMarkNeedsReview()
    {
        var db = TestDb.Create(nameof(OcrWarnings_ShouldAtLeastMarkNeedsReview));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var ocrRecord = TestServiceFactory.SeedOcrRecord(
            db,
            user,
            course,
            PhotoSolutionOcrRecordStatus.Confirmed,
            recognizedProblemText: "旧题目",
            recognizedStudentSolutionText: "旧解答",
            confirmedProblemText: "已知函数 f(x)=x^2+1，求当 x→0 时的极限",
            confirmedStudentSolutionText: "1",
            confirmedFormulasJson: "[]");
        ocrRecord.WarningsJson = "[\"unclear formula\"]";
        db.SaveChanges();

        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user, new FakeSuccessfulLlmResponseParser());

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "前端临时题目",
            StudentSolutionText = "前端临时解答",
            AnalysisMode = "review_solution",
            UserId = user.Id,
            OcrRecordId = ocrRecord.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var analysisResult = await db.AnalysisResults.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(AnswerReliability.NeedsReview, analysisResult.AnswerReliability);
        Assert.True(analysisResult.NeedsReview);
    }

    [Fact]
    public async Task NormalAnalysis_ShouldBeReliable()
    {
        var db = TestDb.Create(nameof(NormalAnalysis_ShouldBeReliable));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user, new FakeSuccessfulLlmResponseParser());

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "已知函数 f(x)=x^2+1，求当 x→0 时的极限",
            StudentSolutionText = "1",
            AnalysisMode = "review_solution",
            UserId = user.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var analysisResult = await db.AnalysisResults.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(AnswerReliability.Reliable, analysisResult.AnswerReliability);
        Assert.False(analysisResult.NeedsReview);
    }
}
