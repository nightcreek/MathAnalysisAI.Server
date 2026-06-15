using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Models;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class UserIsolationAndPersistenceTests
{
    [Fact]
    public async Task UserB_ShouldNotConfirmUserAOcrRecord()
    {
        var db = TestDb.Create(nameof(UserB_ShouldNotConfirmUserAOcrRecord));
        var userA = TestServiceFactory.SeedUser(db, 1, "alice");
        var userB = TestServiceFactory.SeedUser(db, 2, "bob");
        var course = TestServiceFactory.SeedCourse(db);
        var record = TestServiceFactory.SeedOcrRecord(db, userA, course, PhotoSolutionOcrRecordStatus.PendingReview);
        var controller = TestServiceFactory.CreatePhotoSolutionsController(db, new FakePhotoSolutionOcrProvider(), userB);

        var result = await controller.Confirm(record.Id, new ConfirmPhotoSolutionOcrRequestDto
        {
            ProblemText = "设 f(x)=x^2，求极限",
            StudentSolutionText = "x^2"
        }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, ((ObjectResult)result.Result!).StatusCode);
        var persisted = await db.PhotoSolutionOcrRecords.FindAsync(record.Id);
        Assert.Equal(PhotoSolutionOcrRecordStatus.PendingReview, persisted!.Status);
        Assert.Null(persisted.ConfirmedAt);
    }

    [Fact]
    public async Task UserB_ShouldNotAnalyzeUserAOcrRecord()
    {
        var db = TestDb.Create(nameof(UserB_ShouldNotAnalyzeUserAOcrRecord));
        var userA = TestServiceFactory.SeedUser(db, 1, "alice");
        var userB = TestServiceFactory.SeedUser(db, 2, "bob");
        var course = TestServiceFactory.SeedCourse(db);
        var record = TestServiceFactory.SeedOcrRecord(
            db,
            userA,
            course,
            PhotoSolutionOcrRecordStatus.Confirmed,
            confirmedProblemText: "设 f(x)=x^2，求极限",
            confirmedStudentSolutionText: "x^2",
            confirmedFormulasJson: "[]");

        var controller = TestServiceFactory.CreateLearningAnalysisController(db, userB);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
            ProblemText = "手动文本",
            AnalysisMode = "review_solution",
            UserId = userB.Id,
            OcrRecordId = record.Id
        }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, ((ObjectResult)result.Result!).StatusCode);
        Assert.Empty(db.Problems.Where(x => x.CreatedByUserId == userB.Id && x.PhotoSolutionOcrRecordId == record.Id));
        Assert.Empty(db.AnalysisResults.Where(x => x.PhotoSolutionOcrRecordId == record.Id));
    }

    [Fact]
    public async Task SuccessfulAnalysis_ShouldPersistOwnOcrRecordId()
    {
        var db = TestDb.Create(nameof(SuccessfulAnalysis_ShouldPersistOwnOcrRecordId));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var record = TestServiceFactory.SeedOcrRecord(
            db,
            user,
            course,
            PhotoSolutionOcrRecordStatus.Confirmed,
            confirmedProblemText: "设 f(x)=x^2，求极限",
            confirmedStudentSolutionText: "x^2",
            confirmedFormulasJson: "[]");

        var controller = TestServiceFactory.CreateLearningAnalysisController(db, user);

        var result = await controller.Analyze(new AnalysisRequestDto
        {
            CourseId = course.Id,
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
        Assert.Equal(user.Id, problem.CreatedByUserId);
    }

    [Fact]
    public async Task ManualInput_ShouldKeepOcrFieldsNull()
    {
        var db = TestDb.Create(nameof(ManualInput_ShouldKeepOcrFieldsNull));
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
        var problem = await db.Problems.OrderByDescending(x => x.Id).FirstAsync();
        var analysisResult = await db.AnalysisResults.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Null(problem.PhotoSolutionOcrRecordId);
        Assert.Null(analysisResult.PhotoSolutionOcrRecordId);
    }
}
