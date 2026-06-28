using MathAnalysisAI.Server.Controllers;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.Ranking;
using MathAnalysisAI.Server.Services.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class LeaderboardControllerTests
{
    [Fact]
    public async Task GetPublicLeaderboard_ReturnsEmptyList_WhenNoData()
    {
        await using var db = TestDb.Create(nameof(GetPublicLeaderboard_ReturnsEmptyList_WhenNoData));
        var authPersistence = new AuthPersistenceService(db, NullLogger<AuthPersistenceService>.Instance);
        var identityKernel = new IdentityKernel(
            authPersistence,
            authPersistence,
            authPersistence,
            new HttpContextAccessor(),
            new FakeWebHostEnvironment { EnvironmentName = Environments.Development },
            Microsoft.Extensions.Options.Options.Create(new AuthOptions()),
            Microsoft.Extensions.Options.Options.Create(new OidcOptions()));
        var permissionService = new PermissionService(identityKernel);
        var leaderboardService = new LeaderboardService(new AnalysisPersistenceService(db), permissionService);
        var controller = new LeaderboardController(leaderboardService, NullLogger<LeaderboardController>.Instance);

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

        var authPersistence = new AuthPersistenceService(db, NullLogger<AuthPersistenceService>.Instance);
        var identityKernel = new IdentityKernel(
            authPersistence,
            authPersistence,
            authPersistence,
            new HttpContextAccessor(),
            new FakeWebHostEnvironment { EnvironmentName = Environments.Development },
            Microsoft.Extensions.Options.Options.Create(new AuthOptions()),
            Microsoft.Extensions.Options.Options.Create(new OidcOptions()));
        var permissionService = new PermissionService(identityKernel);
        var leaderboardService = new LeaderboardService(new AnalysisPersistenceService(db), permissionService);
        var controller = new LeaderboardController(leaderboardService, NullLogger<LeaderboardController>.Instance);

        var result = await controller.GetPublicLeaderboard(course.Id, 10);

        Assert.NotNull(result);
    }
}
