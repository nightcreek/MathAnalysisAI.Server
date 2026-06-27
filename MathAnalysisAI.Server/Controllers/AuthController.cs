using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Auth;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(
        AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("info")]
    [AllowAnonymous]
    public ActionResult GetAuthInfo()
    {
        return Ok(_authService.GetAuthInfo());
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicies.AuthenticatedUser)]
    public async Task<ActionResult<CurrentUserDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var result = await _authService.GetCurrentUserAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthTokenResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _authService.LoginAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthTokenResponseDto>> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _authService.RegisterAsync(request, cancellationToken);
        if (result.Success && result.Created)
        {
            return CreatedAtAction(nameof(GetCurrentUser), null, result.Value);
        }

        return ToActionResult(result);
    }

    [HttpPut("password")]
    [Authorize(Policy = AuthPolicies.AuthenticatedUser)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _authService.ChangePasswordAsync(request, cancellationToken);
        if (!result.Success)
        {
            return ToErrorResult(result);
        }

        return Ok(new { success = true });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        return Ok(new { success = true });
    }

    [HttpPost("impersonate")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public IActionResult Impersonate([FromBody] ImpersonateRequestDto request)
    {
        var result = _authService.ValidateImpersonation(request.Role);
        if (!result.Success)
        {
            return ToErrorResult(result);
        }

        if (string.IsNullOrWhiteSpace(result.Value))
        {
            return Ok(new { role = (string?)null, message = "Impersonation cleared. Back to admin view." });
        }

        return Ok(new { role = result.Value, message = $"Now viewing as {result.Value}." });
    }

    [HttpPost("join-class")]
    [Authorize(Policy = AuthPolicies.AuthenticatedUser)]
    public async Task<ActionResult<CurrentUserDto>> JoinClass([FromBody] JoinClassRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.JoinClassAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    private ActionResult<T> ToActionResult<T>(AuthServiceResult<T> result)
    {
        if (!result.Success)
        {
            return ToErrorResult(result);
        }

        return result.StatusCode == StatusCodes.Status200OK
            ? Ok(result.Value)
            : StatusCode(result.StatusCode, result.Value);
    }

    private ObjectResult ToErrorResult<T>(AuthServiceResult<T> result)
    {
        return StatusCode(
            result.StatusCode,
            new ApiErrorResponse
            {
                ErrorCode = result.ErrorCode ?? "AUTH_ERROR",
                Message = result.Message ?? "Authentication failed.",
                TraceId = HttpContext?.TraceIdentifier ?? string.Empty,
                IsRetryable = result.StatusCode >= 500
            });
    }
}
