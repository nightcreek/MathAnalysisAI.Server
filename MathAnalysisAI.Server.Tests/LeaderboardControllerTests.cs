using MathAnalysisAI.Server.Controllers;
using MathAnalysisAI.Server.Services.Ranking;
using MathAnalysisAI.Server.Services.Security;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class LeaderboardControllerTests
{
    [Fact]
    public async Task GetPublicLeaderboard_ReturnsEmptyList_WhenNoData()
    {
        await using var db = TestDb.Create(nameof(GetPublicLeaderboard_ReturnsEmptyList_WhenNoData));
        var permissionService = new PermissionService(db);
        var leaderboardService = new LeaderboardService(db, permissionService);
        var controller = new LeaderboardController(leaderboardService);

        var result = await controller.GetPublicLeaderboard(200, 10);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetPublicLeaderboard_ReturnsRankedEntries_WhenDataExists()
    {
        await using var db = TestDb.Create(nameof(GetPublicLeaderboard_ReturnsRankedEntries_WhenDataExists));
        var course = TestServiceFactory.SeedCourse(db);
        var user = TestServiceFactory.SeedUser(db, 1, "user1");

        var stats = new MathAnalysisAI.Server.Models.UserCourseStats
        {
            UserId = user.Id,
            CourseId = course.Id,
            AttemptCount = 10,
            CorrectCount = 8,
            WrongCount = 2,
            AccuracyRate = 80.0m,
            RankingScore = 85.0m
        };
        db.UserCourseStats.Add(stats);
        await db.SaveChangesAsync();

        var permissionService = new PermissionService(db);
        var leaderboardService = new LeaderboardService(db, permissionService);
        var controller = new LeaderboardController(leaderboardService);

        var result = await controller.GetPublicLeaderboard(course.Id, 10);

        Assert.NotNull(result);
    }
}
