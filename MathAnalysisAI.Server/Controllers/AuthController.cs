using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Auth;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string SessionUserIdKey = "auth_user_id";

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly AuthOptions _authOptions;
    private readonly IUserContext _userContext;

    public AuthController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        IOptions<AuthOptions> authOptions,
        IUserContext userContext)
    {
        _db = db;
        _environment = environment;
        _authOptions = authOptions.Value ?? new AuthOptions();
        _userContext = userContext;
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (user != null)
        {
            return Ok(MapToCurrentUser(user));
        }

        return Unauthorized(new
        {
            message = "Not logged in.",
            errorCode = "auth_not_logged_in",
            isRetryable = false
        });
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var mode = _authOptions.GetNormalizedMode();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new
            {
                message = "Username is required.",
                errorCode = "auth_username_required",
                isRetryable = false
            });
        }

        var user = await _db.AppUsers
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password.",
                errorCode = "auth_invalid_credentials",
                isRetryable = false
            });
        }

        if (_authOptions.IsLocalPasswordMode())
        {
            return HandleLocalPasswordLogin(user, request.Password);
        }

        if (_authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment())
        {
            HttpContext.Session.SetInt32(SessionUserIdKey, user.Id);
            return Ok(MapToCurrentUser(user));
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, BuildAuthModeError(mode));
    }

    [HttpPost("register")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserDto>> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!CanRegister())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Registration is not available in the current deployment mode.",
                errorCode = "auth_registration_disabled",
                isRetryable = false
            });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new
            {
                message = "Username is required.",
                errorCode = "auth_username_required",
                isRetryable = false
            });
        }

        var password = request.Password?.Trim() ?? string.Empty;
        var passwordError = ValidatePasswordStrength(password);
        if (passwordError != null)
        {
            return BadRequest(new
            {
                message = passwordError,
                errorCode = "auth_weak_password",
                isRetryable = false
            });
        }

        var existing = await _db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Username == username, cancellationToken);

        if (existing)
        {
            return Conflict(new
            {
                message = "Username is already taken.",
                errorCode = "auth_username_taken",
                isRetryable = false
            });
        }

        var user = new AppUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, _authOptions.BcryptWorkFactor),
            RealName = request.RealName?.Trim(),
            StudentNumber = request.StudentNumber?.Trim(),
            Role = AppUserRole.Student,
            SchoolName = request.SchoolName?.Trim(),
            DepartmentName = request.DepartmentName?.Trim(),
            ClassName = request.ClassName?.Trim()
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        HttpContext.Session.SetInt32(SessionUserIdKey, user.Id);
        return CreatedAtAction(nameof(GetCurrentUser), null, MapToCurrentUser(user));
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (!_authOptions.IsLocalPasswordMode() && !(_authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment()))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Password management is not available in the current deployment mode.",
                errorCode = "auth_password_not_available",
                isRetryable = false
            });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return Unauthorized(new
            {
                message = "You must be logged in to change your password.",
                errorCode = "auth_not_logged_in",
                isRetryable = false
            });
        }

        var newPassword = request.NewPassword?.Trim() ?? string.Empty;
        var passwordError = ValidatePasswordStrength(newPassword);
        if (passwordError != null)
        {
            return BadRequest(new
            {
                message = passwordError,
                errorCode = "auth_weak_password",
                isRetryable = false
            });
        }

        currentUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, _authOptions.BcryptWorkFactor);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(SessionUserIdKey);
        return Ok(new { success = true });
    }

    private ActionResult<CurrentUserDto> HandleLocalPasswordLogin(AppUser user, string? password)
    {
        var passwordValue = password?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(passwordValue))
        {
            return BadRequest(new
            {
                message = "Password is required.",
                errorCode = "auth_password_required",
                isRetryable = true
            });
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Unauthorized(new
            {
                message = "This account has no password set. Please contact the administrator.",
                errorCode = "auth_no_password_set",
                isRetryable = false
            });
        }

        var verified = BCrypt.Net.BCrypt.Verify(passwordValue, user.PasswordHash);
        if (!verified)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password.",
                errorCode = "auth_invalid_credentials",
                isRetryable = false
            });
        }

        HttpContext.Session.SetInt32(SessionUserIdKey, user.Id);
        return Ok(MapToCurrentUser(user));
    }

    private bool CanRegister()
    {
        if (!_authOptions.AllowRegistration)
        {
            return false;
        }

        var mode = _authOptions.GetNormalizedMode();
        if (_authOptions.IsLocalPasswordMode())
        {
            return true;
        }

        if (_authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment())
        {
            return true;
        }

        return false;
    }

    private string? ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        if (password.Length < _authOptions.MinPasswordLength)
        {
            return $"Password must be at least {_authOptions.MinPasswordLength} characters long.";
        }

        return null;
    }

    private static CurrentUserDto MapToCurrentUser(AppUser user)
    {
        return new CurrentUserDto
        {
            UserId = user.Id,
            Username = user.Username,
            RealName = user.RealName,
            Role = user.Role,
            SchoolName = user.SchoolName,
            DepartmentName = user.DepartmentName,
            ClassName = user.ClassName
        };
    }

    private static object BuildAuthModeError(string mode)
    {
        if (string.Equals(mode, AuthOptions.ModeOidc, StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                message = "当前部署要求统一认证登录，请联系管理员接入 OIDC 登录入口。",
                errorCode = "auth_mode_oidc_not_available",
                isRetryable = false
            };
        }

        if (string.Equals(mode, AuthOptions.ModeLocalPassword, StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                message = "当前部署要求密码登录，但服务器尚未启用该登录入口。",
                errorCode = "auth_mode_local_password_not_available",
                isRetryable = false
            };
        }

        if (string.Equals(mode, AuthOptions.ModeDisabled, StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                message = "当前部署未启用登录入口，请联系管理员。",
                errorCode = "auth_mode_disabled",
                isRetryable = false
            };
        }

        return new
        {
            message = "当前部署未启用开发期用户名登录。",
            errorCode = "auth_mode_unavailable",
            isRetryable = false
        };
    }
}
