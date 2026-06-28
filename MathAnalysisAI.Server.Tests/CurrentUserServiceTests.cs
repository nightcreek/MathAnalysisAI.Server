using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class CurrentUserServiceTests
{
    [Fact]
    public async Task GetCurrentUser_ShouldNotUseDevelopmentFallback_WhenModeIsDisabled()
    {
        using var db = TestDb.Create(nameof(GetCurrentUser_ShouldNotUseDevelopmentFallback_WhenModeIsDisabled));
        var user = TestServiceFactory.SeedUser(db, 1, "test_student");
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new FakeSessionFeature());
        var authPersistence = new AuthPersistenceService(db, NullLogger<AuthPersistenceService>.Instance);
        var identityKernel = new IdentityKernel(
            authPersistence,
            authPersistence,
            authPersistence,
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeWebHostEnvironment { EnvironmentName = Environments.Development },
            Microsoft.Extensions.Options.Options.Create(new AuthOptions
            {
                Mode = AuthOptions.ModeDisabled,
                EnableDevelopmentFallback = true,
                DevelopmentFallbackUser = user.Username
            }),
            Microsoft.Extensions.Options.Options.Create(new OidcOptions()));

        var service = new CurrentUserService(identityKernel);

        var currentUser = await service.GetCurrentUserAsync(CancellationToken.None);

        Assert.Null(currentUser);
    }

    [Fact]
    public async Task GetCurrentUser_ShouldUseDevelopmentFallback_WhenModeIsDevelopmentUsername()
    {
        using var db = TestDb.Create(nameof(GetCurrentUser_ShouldUseDevelopmentFallback_WhenModeIsDevelopmentUsername));
        var user = TestServiceFactory.SeedUser(db, 1, "test_student");
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new FakeSessionFeature());
        var authPersistence = new AuthPersistenceService(db, NullLogger<AuthPersistenceService>.Instance);
        var identityKernel = new IdentityKernel(
            authPersistence,
            authPersistence,
            authPersistence,
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeWebHostEnvironment { EnvironmentName = Environments.Development },
            Microsoft.Extensions.Options.Options.Create(new AuthOptions
            {
                Mode = AuthOptions.ModeDevelopmentUsername,
                EnableDevelopmentFallback = true,
                DevelopmentFallbackUser = user.Username
            }),
            Microsoft.Extensions.Options.Options.Create(new OidcOptions()));

        var service = new CurrentUserService(identityKernel);

        var currentUser = await service.GetCurrentUserAsync(CancellationToken.None);

        Assert.NotNull(currentUser);
        Assert.Equal(user.Id, currentUser!.Id);
    }
}
