using MathAnalysisAI.Server.Controllers;
using MathAnalysisAI.Server.DTOs.Auth;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Login_ShouldReject_WhenAuthModeDisabled()
    {
        using var db = TestDb.Create(nameof(Login_ShouldReject_WhenAuthModeDisabled));
        var user = TestServiceFactory.SeedUser(db, 1, "test_student");
        var controller = TestServiceFactory.CreateAuthController(
            db,
            new FakeUserContext { CurrentUser = user },
            Environments.Production,
            new Dictionary<string, string?> { ["Auth:Mode"] = AuthOptions.ModeDisabled });

        var result = await controller.Login(new LoginRequestDto { Username = user.Username }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var payload = Assert.IsAssignableFrom<object>(objectResult.Value);
        Assert.Contains("auth_mode_disabled", payload.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ShouldReject_WhenAuthModeLocalPassword_NoPasswordProvided()
    {
        using var db = TestDb.Create(nameof(Login_ShouldReject_WhenAuthModeLocalPassword_NoPasswordProvided));
        var user = TestServiceFactory.SeedUser(db, 1, "test_student");
        var controller = TestServiceFactory.CreateAuthController(
            db,
            new FakeUserContext { CurrentUser = user },
            Environments.Production,
            new Dictionary<string, string?> { ["Auth:Mode"] = AuthOptions.ModeLocalPassword });

        var result = await controller.Login(new LoginRequestDto { Username = user.Username }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Contains("auth_password_required", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ShouldSucceed_WhenAuthModeLocalPassword_ValidCredentials()
    {
        using var db = TestDb.Create(nameof(Login_ShouldSucceed_WhenAuthModeLocalPassword_ValidCredentials));
        var password = "test_password123";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = TestServiceFactory.SeedUser(db, 1, "test_student", passwordHash);
        var controller = TestServiceFactory.CreateAuthController(
            db,
            new FakeUserContext(),
            Environments.Production,
            new Dictionary<string, string?> { ["Auth:Mode"] = AuthOptions.ModeLocalPassword });

        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new FakeSessionFeature());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Login(new LoginRequestDto { Username = user.Username, Password = password }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CurrentUserDto>(ok.Value);
        Assert.Equal(user.Id, dto.UserId);
        Assert.Equal(user.Username, dto.Username);
        Assert.Equal(user.Id, httpContext.Session.GetInt32("auth_user_id"));
    }

    [Fact]
    public async Task Login_ShouldReject_WhenAuthModeLocalPassword_WrongPassword()
    {
        using var db = TestDb.Create(nameof(Login_ShouldReject_WhenAuthModeLocalPassword_WrongPassword));
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct_password");
        var user = TestServiceFactory.SeedUser(db, 1, "test_student", passwordHash);
        var controller = TestServiceFactory.CreateAuthController(
            db,
            new FakeUserContext { CurrentUser = user },
            Environments.Production,
            new Dictionary<string, string?> { ["Auth:Mode"] = AuthOptions.ModeLocalPassword });

        var result = await controller.Login(new LoginRequestDto { Username = user.Username, Password = "wrong_password" }, CancellationToken.None);

        var unauth = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauth.StatusCode);
        Assert.Contains("auth_invalid_credentials", unauth.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ShouldAllow_DevelopmentUsernameModeInDevelopment()
    {
        using var db = TestDb.Create(nameof(Login_ShouldAllow_DevelopmentUsernameModeInDevelopment));
        var user = TestServiceFactory.SeedUser(db, 1, "test_student");
        var controller = TestServiceFactory.CreateAuthController(
            db,
            new FakeUserContext(),
            Environments.Development,
            new Dictionary<string, string?> { ["Auth:Mode"] = AuthOptions.ModeDevelopmentUsername });

        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new FakeSessionFeature());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Login(new LoginRequestDto { Username = user.Username }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CurrentUserDto>(ok.Value);
        Assert.Equal(user.Id, dto.UserId);
        Assert.Equal(user.Username, dto.Username);
        Assert.Equal(user.Id, httpContext.Session.GetInt32("auth_user_id"));
    }
}
