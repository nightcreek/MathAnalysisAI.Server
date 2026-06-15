using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Models;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class PhotoSolutionsControllerTests
{
    public static IEnumerable<object[]> ManualReviewCases()
    {
        yield return new object[]
        {
            "confidence null",
            "confidence_missing",
            new PhotoSolutionOcrResponseDto
            {
                ProblemText = "设 f(x)=x^2，求极限",
                StudentSolutionText = "x^2",
                Confidence = null,
                Warnings = new List<string>(),
                Formulas = new List<FormulaCandidateDto>(),
                DetectedSections = new List<DetectedSectionDto>(),
                RawProvider = "fake",
                ModelName = "fake"
            }
        };

        yield return new object[]
        {
            "low confidence",
            "confidence_below_threshold:0.85",
            new PhotoSolutionOcrResponseDto
            {
                ProblemText = "设 f(x)=x^2，求极限",
                StudentSolutionText = "x^2",
                Confidence = 0.84m,
                Warnings = new List<string>(),
                Formulas = new List<FormulaCandidateDto>(),
                DetectedSections = new List<DetectedSectionDto>(),
                RawProvider = "fake",
                ModelName = "fake"
            }
        };

        yield return new object[]
        {
            "warnings present",
            "warnings_present",
            new PhotoSolutionOcrResponseDto
            {
                ProblemText = "设 f(x)=x^2，求极限",
                StudentSolutionText = "x^2",
                Confidence = 0.95m,
                Warnings = new List<string> { "uncertain formula" },
                Formulas = new List<FormulaCandidateDto>(),
                DetectedSections = new List<DetectedSectionDto>(),
                RawProvider = "fake",
                ModelName = "fake"
            }
        };

        yield return new object[]
        {
            "contains unclear",
            "contains_unclear",
            new PhotoSolutionOcrResponseDto
            {
                ProblemText = "设 f(x)=x^2，求[unclear]",
                StudentSolutionText = "x^2",
                Confidence = 0.95m,
                Warnings = new List<string>(),
                Formulas = new List<FormulaCandidateDto>(),
                DetectedSections = new List<DetectedSectionDto>(),
                RawProvider = "fake",
                ModelName = "fake"
            }
        };

        yield return new object[]
        {
            "problem too short",
            "problem_text_too_short",
            new PhotoSolutionOcrResponseDto
            {
                ProblemText = "x^2",
                StudentSolutionText = "x^2",
                Confidence = 0.95m,
                Warnings = new List<string>(),
                Formulas = new List<FormulaCandidateDto>(),
                DetectedSections = new List<DetectedSectionDto>(),
                RawProvider = "fake",
                ModelName = "fake"
            }
        };
    }

    [Fact]
    public async Task OcrResult_IsPersisted_And_ReturnsAuditFields()
    {
        var db = TestDb.Create(nameof(OcrResult_IsPersisted_And_ReturnsAuditFields));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);        var provider = new FakePhotoSolutionOcrProvider
        {
            Response = new PhotoSolutionOcrResponseDto
            {
                ProblemText = "设 f(x)=x^2，求极限",
                StudentSolutionText = "x^2",
                Confidence = 0.95m,
                Warnings = new List<string>(),
                Formulas = new List<FormulaCandidateDto>
                {
                    new() { Latex = "x^2" }
                },
                DetectedSections = new List<DetectedSectionDto>
                {
                    new() { Type = "problem", Content = "设 f(x)=x^2，求极限" }
                },
                RawProvider = "fake",
                ModelName = "fake-model"
            }
        };
        var controller = TestServiceFactory.CreatePhotoSolutionsController(db, provider, user);

        var result = await controller.Ocr(course.Id, null, null, TestServiceFactory.CreateFormFile("sample.png"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PhotoSolutionOcrResponseDto>(ok.Value);
        Assert.NotNull(payload.OcrRecordId);
        Assert.Equal(PhotoSolutionOcrRecordStatus.PendingReview, payload.Status);
        Assert.False(payload.NeedsManualReview);
        Assert.False(payload.CanAnalyze);
        Assert.False(payload.IsConfirmed);
        Assert.NotNull(payload.ReviewReasons);

        var record = await db.PhotoSolutionOcrRecords.FindAsync(payload.OcrRecordId.Value);
        Assert.NotNull(record);
        Assert.Equal(user.Id, record!.UserId);
        Assert.Equal(course.Id, record.CourseId);
        Assert.Equal("sample.png", record.OriginalFileName);
        Assert.Equal("image/png", record.ContentType);
        Assert.Equal(4, record.FileSizeBytes);
        Assert.Equal("fake", record.OcrProvider);
    }

    [Theory]
    [MemberData(nameof(ManualReviewCases))]
    public async Task OcrResult_ShouldRequireManualReview_WhenRuleMatches(string caseName, string expectedReason, PhotoSolutionOcrResponseDto recognition)
    {
        var db = TestDb.Create($"{nameof(OcrResult_ShouldRequireManualReview_WhenRuleMatches)}_{caseName}");
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);        var provider = new FakePhotoSolutionOcrProvider { Response = recognition };
        var controller = TestServiceFactory.CreatePhotoSolutionsController(db, provider, user);

        var result = await controller.Ocr(course.Id, null, null, TestServiceFactory.CreateFormFile(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PhotoSolutionOcrResponseDto>(ok.Value);
        Assert.True(payload.NeedsManualReview);
        Assert.Equal(PhotoSolutionOcrRecordStatus.NeedsManualReview, payload.Status);
        Assert.False(payload.CanAnalyze);
        Assert.Contains(expectedReason, payload.ReviewReasons);

        var record = await db.PhotoSolutionOcrRecords.FindAsync(payload.OcrRecordId!.Value);
        Assert.NotNull(record);
        Assert.Equal(payload.Status, record!.Status);
    }

    [Fact]
    public async Task Confirm_ShouldPersistSnapshot_AndFlipStatusToConfirmed()
    {
        var db = TestDb.Create(nameof(Confirm_ShouldPersistSnapshot_AndFlipStatusToConfirmed));
        var user = TestServiceFactory.SeedUser(db, 1, "alice");
        var course = TestServiceFactory.SeedCourse(db);
        var record = TestServiceFactory.SeedOcrRecord(db, user, course, PhotoSolutionOcrRecordStatus.PendingReview);
        var controller = TestServiceFactory.CreatePhotoSolutionsController(db, new FakePhotoSolutionOcrProvider(), user);

        var result = await controller.Confirm(record.Id, new ConfirmPhotoSolutionOcrRequestDto
        {
            ProblemText = "设 f(x)=x^2，求极限",
            StudentSolutionText = "x^2",
            Formulas = new List<FormulaCandidateDto>
            {
                new() { Latex = "x^2", Context = "problem" }
            }
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PhotoSolutionOcrResponseDto>(ok.Value);
        Assert.True(payload.IsConfirmed);
        Assert.Equal(PhotoSolutionOcrRecordStatus.Confirmed, payload.Status);
        Assert.True(payload.CanAnalyze);

        var persisted = await db.PhotoSolutionOcrRecords.FindAsync(record.Id);
        Assert.NotNull(persisted);
        Assert.Equal("设 f(x)=x^2，求极限", persisted!.ConfirmedProblemText);
        Assert.Equal("x^2", persisted.ConfirmedStudentSolutionText);
        Assert.Equal(user.Id, persisted.ConfirmedByUserId);
        Assert.Equal(PhotoSolutionOcrRecordStatus.Confirmed, persisted.Status);
        Assert.NotNull(persisted.ConfirmedAt);
        Assert.Contains("x^2", persisted.ConfirmedFormulasJson);
    }
}
