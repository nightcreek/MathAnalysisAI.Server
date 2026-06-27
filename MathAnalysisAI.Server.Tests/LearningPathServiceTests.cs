using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Knowledge;
using MathAnalysisAI.Server.Data.Knowledge;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class LearningPathServiceTests
{
    [Fact]
    public async Task BuildLearningPathAsync_ReturnsEmptyItems_ForNewUser()
    {
        await using var db = TestDb.Create(nameof(BuildLearningPathAsync_ReturnsEmptyItems_ForNewUser));
        var course = TestServiceFactory.SeedCourse(db);
        var user = TestServiceFactory.SeedUser(db, 1, "student1");
        IQuestionModule questionSvc = new QuestionService(new AnalysisPersistenceService(db));
        var svc = new LearningPathService(new LearningPathPersistenceService(db), questionSvc);

        var result = await svc.BuildLearningPathAsync(course.Id, user.Id);

        Assert.NotNull(result);
        Assert.Equal(course.Id, result.CourseId);
        Assert.Equal(course.Name, result.CourseName);
        Assert.Equal(0, result.TotalKnowledgePoints);
        Assert.Equal(0, result.MasteredCount);
        Assert.Empty(result.RecommendedOrder);
        Assert.Empty(result.WeakPoints);
    }

    [Fact]
    public async Task BuildLearningPathAsync_IncludesKnowledgePoints_WhenPresent()
    {
        await using var db = TestDb.Create(nameof(BuildLearningPathAsync_IncludesKnowledgePoints_WhenPresent));
        var course = TestServiceFactory.SeedCourse(db);
        var user = TestServiceFactory.SeedUser(db, 1, "student1");

        var chapter = new Chapter
        {
            Id = 1301,
            CourseId = course.Id,
            Name = "Limits",
            Code = "CH01",
            OrderIndex = 1
        };
        db.Chapters.Add(chapter);

        var kp = new KnowledgePoint
        {
            Id = 10001,
            Name = "L'Hôpital's Rule",
            Code = "KP001",
            ChapterId = chapter.Id,
            CourseId = course.Id
        };
        db.KnowledgePoints.Add(kp);
        await db.SaveChangesAsync();

        IQuestionModule questionSvc = new QuestionService(new AnalysisPersistenceService(db));
        var svc = new LearningPathService(new LearningPathPersistenceService(db), questionSvc);

        var result = await svc.BuildLearningPathAsync(course.Id, user.Id);

        Assert.Equal(1, result.TotalKnowledgePoints);
        Assert.Single(result.RecommendedOrder);
        Assert.Equal("L'Hôpital's Rule", result.RecommendedOrder[0].Name);
    }

    [Fact]
    public async Task BuildLearningPathAsync_MarksMasteredKnowledgePoints()
    {
        await using var db = TestDb.Create(nameof(BuildLearningPathAsync_MarksMasteredKnowledgePoints));
        var course = TestServiceFactory.SeedCourse(db);
        var user = TestServiceFactory.SeedUser(db, 1, "student1");

        var kp = new KnowledgePoint
        {
            Id = 10001,
            Name = "Limits",
            Code = "KP001",
            CourseId = course.Id
        };
        db.KnowledgePoints.Add(kp);

        var state = new UserKnowledgeState
        {
            UserId = user.Id,
            KnowledgePointId = kp.Id,
            MasteryLevel = 85,
            PracticeCount = 10,
            CorrectCount = 9
        };
        db.UserKnowledgeStates.Add(state);
        await db.SaveChangesAsync();

        IQuestionModule questionSvc = new QuestionService(new AnalysisPersistenceService(db));
        var svc = new LearningPathService(new LearningPathPersistenceService(db), questionSvc);

        var result = await svc.BuildLearningPathAsync(course.Id, user.Id);

        Assert.Equal(1, result.MasteredCount);
        Assert.Equal(85, result.RecommendedOrder[0].MasteryLevel);
    }

    [Fact]
    public async Task BuildLearningPathAsync_ReturnsLearningPath_WithKnowledgeStates()
    {
        await using var db = TestDb.Create(nameof(BuildLearningPathAsync_ReturnsLearningPath_WithKnowledgeStates));
        var course = TestServiceFactory.SeedCourse(db);
        var user = TestServiceFactory.SeedUser(db, 1, "student1");

        var problem = new Problem
        {
            Id = 5001,
            CourseId = course.Id,
            ContentMarkdown = "Test problem",
            ProblemType = "mixed",
            SourceType = "test"
        };
        db.Problems.Add(problem);

        var kp = new KnowledgePoint
        {
            Id = 10001,
            Name = "Integration",
            Code = "KP001",
            CourseId = course.Id
        };
        db.KnowledgePoints.Add(kp);

        var solution = new StudentSolution
        {
            Id = 5001,
            ProblemId = problem.Id,
            UserId = user.Id,
            SolutionText = "Test solution",
            SubmittedAt = DateTime.UtcNow
        };
        db.StudentSolutions.Add(solution);

        var analysisResult = new AnalysisResult
        {
            Id = 5001,
            ProblemId = problem.Id,
            StudentSolutionId = solution.Id,
            AnalysisMode = "review_solution",
            CreatedAt = DateTime.UtcNow
        };
        db.AnalysisResults.Add(analysisResult);

        await db.SaveChangesAsync();

        var mistake = new MistakeRecord
        {
            Id = 5001,
            AnalysisResultId = analysisResult.Id,
            KnowledgePointId = kp.Id,
            MistakeTag = "计算错误",
            Severity = 3
        };
        db.MistakeRecords.Add(mistake);

        var state = new UserKnowledgeState
        {
            UserId = user.Id,
            KnowledgePointId = kp.Id,
            MasteryLevel = 30,
            PracticeCount = 5,
            CorrectCount = 1
        };
        db.UserKnowledgeStates.Add(state);
        await db.SaveChangesAsync();

        IQuestionModule questionSvc = new QuestionService(new AnalysisPersistenceService(db));
        var svc = new LearningPathService(new LearningPathPersistenceService(db), questionSvc);

        var result = await svc.BuildLearningPathAsync(course.Id, user.Id);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalKnowledgePoints);
        Assert.Single(result.RecommendedOrder);
        Assert.Equal("Integration", result.RecommendedOrder[0].Name);
        Assert.True(result.RecommendedOrder[0].MasteryLevel <= 30);
    }

    [Fact]
    public async Task BuildLearningPathAsync_OrdersByPriority()
    {
        await using var db = TestDb.Create(nameof(BuildLearningPathAsync_OrdersByPriority));
        var course = TestServiceFactory.SeedCourse(db);
        var user = TestServiceFactory.SeedUser(db, 1, "student1");

        var kp1 = new KnowledgePoint { Id = 10001, Name = "A", Code = "A", CourseId = course.Id };
        var kp2 = new KnowledgePoint { Id = 10002, Name = "B", Code = "B", CourseId = course.Id };
        db.KnowledgePoints.AddRange(kp1, kp2);

        db.UserKnowledgeStates.Add(new UserKnowledgeState
        {
            UserId = user.Id, KnowledgePointId = kp1.Id,
            MasteryLevel = 90, PracticeCount = 20, CorrectCount = 18
        });

        db.UserKnowledgeStates.Add(new UserKnowledgeState
        {
            UserId = user.Id, KnowledgePointId = kp2.Id,
            MasteryLevel = 10, PracticeCount = 3, CorrectCount = 0
        });
        await db.SaveChangesAsync();

        IQuestionModule questionSvc = new QuestionService(new AnalysisPersistenceService(db));
        var svc = new LearningPathService(new LearningPathPersistenceService(db), questionSvc);

        var result = await svc.BuildLearningPathAsync(course.Id, user.Id);

        Assert.Equal(2, result.RecommendedOrder.Count);
        Assert.Equal("B", result.RecommendedOrder[0].Code);
        Assert.Equal("A", result.RecommendedOrder[1].Code);
    }
}
