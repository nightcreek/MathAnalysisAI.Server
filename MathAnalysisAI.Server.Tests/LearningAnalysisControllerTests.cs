using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Models;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class LearningAnalysisControllerTests
{
    [Fact]
    public async Task Analyze_ShouldReturnConflict_ForPendingOcrRecord()
    {
        var db = TestDb.Create(nameof(Analyze_ShouldReturnConflict_ForPendingOcrRecord));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var record = TestServiceFactory.SeedOcrRecord(db, user, course, PhotoSolutionOcrRecordStatus.PendingReview);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "手动文本",
            AnalysisMode = "review_solution",
            UserId = user.Id,
            OcrRecordId = record.Id
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Analyze_ShouldReturnConflict_ForNeedsManualReviewOcrRecord()
    {
        var db = TestDb.Create(nameof(Analyze_ShouldReturnConflict_ForNeedsManualReviewOcrRecord));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var record = TestServiceFactory.SeedOcrRecord(db, user, course, PhotoSolutionOcrRecordStatus.NeedsManualReview);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "手动文本",
            AnalysisMode = "review_solution",
            UserId = user.Id,
            OcrRecordId = record.Id
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Analyze_ShouldAllowConfirmedOcrRecord_AndUseServerSnapshot()
    {
        var db = TestDb.Create(nameof(Analyze_ShouldAllowConfirmedOcrRecord_AndUseServerSnapshot));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var record = TestServiceFactory.SeedOcrRecord(
            db,
            user,
            course,
            PhotoSolutionOcrRecordStatus.Confirmed,
            recognizedProblemText: "旧题目",
            recognizedStudentSolutionText: "旧解答",
            confirmedProblemText: "设 f(x)=x^2，求极限",
            confirmedStudentSolutionText: "x^2",
            confirmedFormulasJson: "[]");

        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id + 999,
            ProblemText = "前端临时题目",
            StudentSolutionText = "前端临时解答",
            AnalysisMode = "review_solution",
            UserId = user.Id,
            OcrRecordId = record.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
        var problem = await db.Problems.OrderByDescending(x => x.Id).FirstAsync();
        var analysisResult = await db.AnalysisResults.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(record.Id, problem.PhotoSolutionOcrRecordId);
        Assert.Equal(record.Id, analysisResult.PhotoSolutionOcrRecordId);
        Assert.Equal(course.Id, problem.CourseId);
        Assert.Equal("设 f(x)=x^2，求极限", problem.ContentMarkdown);
    }

    [Fact]
    public async Task Analyze_ShouldReturnNotFound_ForMissingOcrRecord()
    {
        var db = TestDb.Create(nameof(Analyze_ShouldReturnNotFound_ForMissingOcrRecord));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = 1,
            ProblemText = "手动文本",
            AnalysisMode = "review_solution",
            UserId = user.Id,
            OcrRecordId = 99999
        }, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains("OCR record not found", notFound.Value!.ToString());
    }

    [Fact]
    public async Task Analyze_ShouldKeepManualInputPathWorking_WhenNoOcrRecordProvided()
    {
        var db = TestDb.Create(nameof(Analyze_ShouldKeepManualInputPathWorking_WhenNoOcrRecordProvided));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "手动文本",
            StudentSolutionText = "手动解答",
            AnalysisMode = "review_solution",
            UserId = user.Id
        }, CancellationToken.None);

        Assert.True(result.Result is OkObjectResult);
    }
}
