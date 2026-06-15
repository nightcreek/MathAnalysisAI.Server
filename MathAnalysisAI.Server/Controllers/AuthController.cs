using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Auth;
using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
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

    /// <summary>
    /// 暴露当前部署使用的认证模式，供前端决定是否显示密码字段。
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public ActionResult GetAuthInfo()
    {
        var mode = _authOptions.GetNormalizedMode();
        return Ok(new
        {
            mode = mode,
            requirePassword = _authOptions.IsLocalPasswordMode(),
            allowRegistration = _authOptions.AllowRegistration
                && (_authOptions.IsLocalPasswordMode() || (_authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment())),
            isDevelopmentEnvironment = _environment.IsDevelopment()
        });
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
        HttpContext.Session.Remove("impersonated_role");
        return Ok(new { success = true });
    }

    [HttpPost("impersonate")]
    public IActionResult Impersonate([FromBody] ImpersonateRequestDto request)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return Unauthorized(new { message = "Not logged in." });

        if (!string.Equals(user.Role, AppUserRole.Admin, StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only admin can impersonate." });

        var role = request.Role?.Trim()?.ToLower();
        var validRoles = new HashSet<string> { "student", "teacher", "admin", "school_leader" };

        if (string.IsNullOrWhiteSpace(role))
        {
            HttpContext.Session.Remove("impersonated_role");
            return Ok(new { role = (string?)null, message = "Impersonation cleared. Back to admin view." });
        }

        if (!validRoles.Contains(role))
            return BadRequest(new { message = $"Invalid role: '{request.Role}'. Allowed: student, teacher, admin, school_leader." });

        if (_userContext is CurrentUserService cus)
            cus.SetImpersonatedRole(role);
        else
            HttpContext.Session.SetString("impersonated_role", role);

        return Ok(new { role, message = $"Now viewing as {role}." });
    }

    [HttpPost("join-class")]
    public async Task<IActionResult> JoinClass([FromBody] JoinClassRequestDto request, CancellationToken cancellationToken)
    {
        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
            return Unauthorized(new { message = "Not logged in." });

        if (string.IsNullOrWhiteSpace(request.TeacherId) && string.IsNullOrWhiteSpace(request.TeacherUsername))
            return BadRequest(new { message = "TeacherId or TeacherUsername is required." });

        AppUser? teacher = null;
        if (!string.IsNullOrWhiteSpace(request.TeacherId) && int.TryParse(request.TeacherId, out var tId) && tId > 0)
            teacher = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == tId
                && (x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin), cancellationToken);
        else if (!string.IsNullOrWhiteSpace(request.TeacherUsername))
            teacher = await _db.AppUsers.FirstOrDefaultAsync(x => x.Username == request.TeacherUsername.Trim()
                && (x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin), cancellationToken);

        if (teacher == null)
            return NotFound(new { message = "Teacher not found." });

        currentUser.TeacherId = teacher.Id;

        if (!string.IsNullOrWhiteSpace(request.RealName))
            currentUser.RealName = request.RealName.Trim();

        if (!string.IsNullOrWhiteSpace(request.StudentNumber))
            currentUser.StudentNumber = request.StudentNumber.Trim();

        if (!string.IsNullOrWhiteSpace(request.SchoolName))
            currentUser.SchoolName = request.SchoolName.Trim();

        if (!string.IsNullOrWhiteSpace(request.ClassName))
            currentUser.ClassName = request.ClassName.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(MapToCurrentUser(currentUser));
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

    private CurrentUserDto MapToCurrentUser(AppUser user)
    {
        string? impersonatedRole = null;
        if (_userContext is CurrentUserService cus)
            impersonatedRole = cus.GetImpersonatedRole();

        return new CurrentUserDto
        {
            UserId = user.Id,
            Username = user.Username,
            RealName = user.RealName,
            StudentNumber = user.StudentNumber,
            Role = user.Role,
            SchoolName = user.SchoolName,
            DepartmentName = user.DepartmentName,
            ClassName = user.ClassName,
            TeacherId = user.TeacherId,
            ImpersonatedRole = impersonatedRole
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
