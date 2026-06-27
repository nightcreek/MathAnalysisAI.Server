using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Knowledge;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class QuestionServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsQuestion_AndReturnsDto()
    {
        await using var db = TestDb.Create(nameof(CreateAsync_PersistsQuestion_AndReturnsDto));
        var course = TestServiceFactory.SeedCourse(db);

        var svc = new QuestionService(new AnalysisPersistenceService(db));
        var request = new MathAnalysisAI.Server.DTOs.Analysis.CreateQuestionRequestDto
        {
            Title = "求极限",
            Content = "\\lim_{x\\to 0}\\frac{\\sin x}{x}",
            Difficulty = "easy",
            QuestionType = "calculation",
            CourseId = course.Id
        };

        var result = await svc.CreateAsync(request, null);

        Assert.NotNull(result);
        Assert.Equal("求极限", result.Title);
        Assert.Equal("easy", result.Difficulty);
        Assert.Equal("calculation", result.QuestionType);
        Assert.Equal(course.Id, result.CourseId);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = TestDb.Create(nameof(GetByIdAsync_ReturnsNull_WhenNotFound));
        var svc = new QuestionService(new AnalysisPersistenceService(db));

        var result = await svc.GetByIdAsync(99999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDto_WhenFound()
    {
        await using var db = TestDb.Create(nameof(GetByIdAsync_ReturnsDto_WhenFound));
        var course = TestServiceFactory.SeedCourse(db);
        var svc = new QuestionService(new AnalysisPersistenceService(db));

        var created = await svc.CreateAsync(new MathAnalysisAI.Server.DTOs.Analysis.CreateQuestionRequestDto
        {
            Title = "Test Question",
            Content = "Solve for x",
            CourseId = course.Id
        }, null);

        var retrieved = await svc.GetByIdAsync(created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Question", retrieved!.Title);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesQuestion_AndReturnsUpdatedDto()
    {
        await using var db = TestDb.Create(nameof(UpdateAsync_ModifiesQuestion_AndReturnsUpdatedDto));
        var course = TestServiceFactory.SeedCourse(db);
        var svc = new QuestionService(new AnalysisPersistenceService(db));

        var created = await svc.CreateAsync(new MathAnalysisAI.Server.DTOs.Analysis.CreateQuestionRequestDto
        {
            Title = "Original",
            Content = "Original content",
            CourseId = course.Id
        }, null);

        var updateRequest = new MathAnalysisAI.Server.DTOs.Analysis.CreateQuestionRequestDto
        {
            Title = "Updated",
            Content = "Updated content",
            Difficulty = "hard",
            CourseId = course.Id
        };

        var updated = await svc.UpdateAsync(created.Id, updateRequest);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated!.Title);
        Assert.Equal("hard", updated.Difficulty);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = TestDb.Create(nameof(UpdateAsync_ReturnsNull_WhenNotFound));
        var course = TestServiceFactory.SeedCourse(db);
        var svc = new QuestionService(new AnalysisPersistenceService(db));

        var result = await svc.UpdateAsync(99999, new MathAnalysisAI.Server.DTOs.Analysis.CreateQuestionRequestDto
        {
            Title = "N/A",
            Content = "N/A",
            CourseId = course.Id
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesQuestion_AndReturnsTrue()
    {
        await using var db = TestDb.Create(nameof(DeleteAsync_RemovesQuestion_AndReturnsTrue));
        var course = TestServiceFactory.SeedCourse(db);
        var svc = new QuestionService(new AnalysisPersistenceService(db));

        var created = await svc.CreateAsync(new MathAnalysisAI.Server.DTOs.Analysis.CreateQuestionRequestDto
        {
            Title = "To Delete",
            Content = "Content",
            CourseId = course.Id
        }, null);

        var deleted = await svc.DeleteAsync(created.Id);
        Assert.True(deleted);

        var after = await svc.GetByIdAsync(created.Id);
        Assert.Null(after);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        await using var db = TestDb.Create(nameof(DeleteAsync_ReturnsFalse_WhenNotFound));
        var svc = new QuestionService(new AnalysisPersistenceService(db));

        var result = await svc.DeleteAsync(99999);
        Assert.False(result);
    }

    [Fact]
    public async Task ListAsync_AppliesFilters()
    {
        await using var db = TestDb.Create(nameof(ListAsync_AppliesFilters));
        var course = TestServiceFactory.SeedCourse(db);
        var svc = new QuestionService(new AnalysisPersistenceService(db));

        await svc.CreateAsync(new MathAnalysisAI.Server.DTOs.Analysis.CreateQuestionRequestDto
        {
            Title = "Easy Question",
            Content = "1+1",
            Difficulty = "easy",
            CourseId = course.Id,
            IsPublished = true
        }, null);

        await svc.CreateAsync(new MathAnalysisAI.Server.DTOs.Analysis.CreateQuestionRequestDto
        {
            Title = "Hard Question",
            Content = "complex integral",
            Difficulty = "hard",
            CourseId = course.Id,
            IsPublished = true
        }, null);

        var all = await svc.ListAsync(null, null, null, null, null, null, false, 1, 20);
        Assert.Equal(2, all.TotalCount);

        var easy = await svc.ListAsync(course.Id, null, null, "easy", null, null, false, 1, 20);
        Assert.Equal(1, easy.TotalCount);
        Assert.Equal("Easy Question", easy.Items[0].Title);

        var search = await svc.ListAsync(null, null, null, null, null, "integral", false, 1, 20);
        Assert.Equal(1, search.TotalCount);
        Assert.Equal("Hard Question", search.Items[0].Title);
    }
}
