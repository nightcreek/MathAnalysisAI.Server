using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Auth;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
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
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly AuthOptions _authOptions;
    private readonly OidcOptions _oidcOptions;
    private readonly IUserContext _userContext;
    private readonly ILocalJwtTokenService _localJwtTokenService;

    public AuthController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        IOptions<AuthOptions> authOptions,
        IOptions<OidcOptions> oidcOptions,
        IUserContext userContext,
        ILocalJwtTokenService localJwtTokenService)
    {
        _db = db;
        _environment = environment;
        _authOptions = authOptions.Value ?? new AuthOptions();
        _oidcOptions = oidcOptions.Value ?? new OidcOptions();
        _userContext = userContext;
        _localJwtTokenService = localJwtTokenService;
    }

    [HttpGet("info")]
    [AllowAnonymous]
    public ActionResult GetAuthInfo()
    {
        var mode = _authOptions.GetNormalizedMode();
        return Ok(new
        {
            mode,
            requirePassword = _authOptions.IsLocalPasswordMode(),
            allowRegistration = CanRegister(),
            isDevelopmentEnvironment = _environment.IsDevelopment(),
            oidc = _authOptions.IsOidcMode()
                ? new
                {
                    authority = _oidcOptions.Authority,
                    audience = _oidcOptions.Audience,
                    clientId = _oidcOptions.ClientId,
                    scopes = _oidcOptions.GetScopeList(),
                    redirectPath = _oidcOptions.RedirectPath,
                    postLogoutRedirectPath = _oidcOptions.PostLogoutRedirectPath,
                    requireHttpsMetadata = _oidcOptions.RequireHttpsMetadata
                }
                : null
        });
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicies.AuthenticatedUser)]
    public async Task<ActionResult<CurrentUserDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (user == null)
        {
            return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_NOT_LOGGED_IN", "Not logged in.");
        }

        return Ok(MapToCurrentUser(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthTokenResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var mode = _authOptions.GetNormalizedMode();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (_authOptions.IsOidcMode() || _authOptions.IsDisabledMode())
        {
            return BuildAuthModeError(mode);
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "AUTH_USERNAME_REQUIRED", "Username is required.");
        }

        var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
        if (user == null)
        {
            return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_INVALID_CREDENTIALS", "Invalid username or password.");
        }

        if (_authOptions.IsLocalPasswordMode())
        {
            var passwordValue = request.Password?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(passwordValue))
            {
                return this.ApiError(StatusCodes.Status400BadRequest, "AUTH_PASSWORD_REQUIRED", "Password is required.");
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(passwordValue, user.PasswordHash))
            {
                return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_INVALID_CREDENTIALS", "Invalid username or password.");
            }

            return Ok(BuildTokenResponse(user));
        }

        if (_authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment())
        {
            return Ok(BuildTokenResponse(user));
        }

        return BuildAuthModeError(mode);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthTokenResponseDto>> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!CanRegister())
        {
            return this.ApiError(
                StatusCodes.Status503ServiceUnavailable,
                "AUTH_REGISTRATION_DISABLED",
                "Registration is not available in the current deployment mode.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "AUTH_USERNAME_REQUIRED", "Username is required.");
        }

        var password = request.Password?.Trim() ?? string.Empty;
        var passwordError = ValidatePasswordStrength(password);
        if (passwordError != null)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "AUTH_WEAK_PASSWORD", passwordError);
        }

        var existing = await _db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Username == username, cancellationToken);
        if (existing)
        {
            return this.ApiError(StatusCodes.Status409Conflict, "AUTH_USERNAME_TAKEN", "Username is already taken.");
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

        return CreatedAtAction(nameof(GetCurrentUser), null, BuildTokenResponse(user));
    }

    [HttpPut("password")]
    [Authorize(Policy = AuthPolicies.AuthenticatedUser)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (!_authOptions.IsLocalPasswordMode() && !(_authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment()))
        {
            return this.ApiError(
                StatusCodes.Status503ServiceUnavailable,
                "AUTH_PASSWORD_NOT_AVAILABLE",
                "Password management is not available in the current deployment mode.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_NOT_LOGGED_IN", "You must be logged in to change your password.");
        }

        var trackedUser = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == currentUser.Id, cancellationToken);
        if (trackedUser == null)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "AUTH_USER_NOT_FOUND", "Current user record was not found.");
        }

        var newPassword = request.NewPassword?.Trim() ?? string.Empty;
        var passwordError = ValidatePasswordStrength(newPassword);
        if (passwordError != null)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "AUTH_WEAK_PASSWORD", passwordError);
        }

        trackedUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, _authOptions.BcryptWorkFactor);
        await _db.SaveChangesAsync(cancellationToken);

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
        var role = request.Role?.Trim()?.ToLowerInvariant();
        var validRoles = new HashSet<string> { "student", "teacher", "admin", "school_leader" };

        if (string.IsNullOrWhiteSpace(role))
        {
            return Ok(new { role = (string?)null, message = "Impersonation cleared. Back to admin view." });
        }

        if (!validRoles.Contains(role))
        {
            return this.ApiError(
                StatusCodes.Status400BadRequest,
                "AUTH_INVALID_IMPERSONATION_ROLE",
                $"Invalid role: '{request.Role}'. Allowed: student, teacher, admin, school_leader.");
        }

        return Ok(new { role, message = $"Now viewing as {role}." });
    }

    [HttpPost("join-class")]
    [Authorize(Policy = AuthPolicies.AuthenticatedUser)]
    public async Task<IActionResult> JoinClass([FromBody] JoinClassRequestDto request, CancellationToken cancellationToken)
    {
        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_NOT_LOGGED_IN", "Not logged in.");
        }

        if (string.IsNullOrWhiteSpace(request.TeacherId) && string.IsNullOrWhiteSpace(request.TeacherUsername))
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "AUTH_TEACHER_REQUIRED", "TeacherId or TeacherUsername is required.");
        }

        var trackedUser = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == currentUser.Id, cancellationToken);
        if (trackedUser == null)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "AUTH_USER_NOT_FOUND", "Current user record was not found.");
        }

        AppUser? teacher = null;
        if (!string.IsNullOrWhiteSpace(request.TeacherId)
            && int.TryParse(request.TeacherId, out var teacherId)
            && teacherId > 0)
        {
            teacher = await _db.AppUsers.FirstOrDefaultAsync(
                x => x.Id == teacherId
                    && (x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin),
                cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.TeacherUsername))
        {
            var teacherUsername = request.TeacherUsername.Trim();
            teacher = await _db.AppUsers.FirstOrDefaultAsync(
                x => x.Username == teacherUsername
                    && (x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin),
                cancellationToken);
        }

        if (teacher == null)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "AUTH_TEACHER_NOT_FOUND", "Teacher not found.");
        }

        trackedUser.TeacherId = teacher.Id;

        if (!string.IsNullOrWhiteSpace(request.RealName))
        {
            trackedUser.RealName = request.RealName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.StudentNumber))
        {
            trackedUser.StudentNumber = request.StudentNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.SchoolName))
        {
            trackedUser.SchoolName = request.SchoolName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.ClassName))
        {
            trackedUser.ClassName = request.ClassName.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapToCurrentUser(trackedUser));
    }

    private ActionResult<AuthTokenResponseDto> BuildAuthModeError(string mode)
    {
        if (string.Equals(mode, AuthOptions.ModeOidc, StringComparison.OrdinalIgnoreCase))
        {
            return this.ApiError(
                StatusCodes.Status503ServiceUnavailable,
                "AUTH_MODE_OIDC_REQUIRED",
                "当前部署要求统一认证登录，请使用 OIDC 登录入口。");
        }

        if (string.Equals(mode, AuthOptions.ModeDisabled, StringComparison.OrdinalIgnoreCase))
        {
            return this.ApiError(
                StatusCodes.Status503ServiceUnavailable,
                "AUTH_MODE_DISABLED",
                "当前部署未启用登录入口，请联系管理员。");
        }

        return this.ApiError(
            StatusCodes.Status503ServiceUnavailable,
            "AUTH_MODE_UNAVAILABLE",
            "当前部署未启用该登录入口。");
    }

    private bool CanRegister()
    {
        if (!_authOptions.AllowRegistration)
        {
            return false;
        }

        if (_authOptions.IsLocalPasswordMode())
        {
            return true;
        }

        return _authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment();
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

    private AuthTokenResponseDto BuildTokenResponse(AppUser user)
    {
        var token = _localJwtTokenService.IssueToken(user);
        return new AuthTokenResponseDto
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = MapToCurrentUser(user)
        };
    }

    private CurrentUserDto MapToCurrentUser(AppUser user)
    {
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
            ImpersonatedRole = _userContext.GetImpersonatedRole()
        };
    }
}
