using MathAnalysisAI.Server.DTOs.Auth;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using Microsoft.Extensions.Options;

namespace MathAnalysisAI.Server.Services.Auth;

public sealed class AuthService
{
    private readonly IAuthPersistenceService _authPersistenceService;
    private readonly IWebHostEnvironment _environment;
    private readonly AuthOptions _authOptions;
    private readonly OidcOptions _oidcOptions;
    private readonly IUserContext _userContext;
    private readonly ILocalJwtTokenService _localJwtTokenService;

    public AuthService(
        IAuthPersistenceService authPersistenceService,
        IWebHostEnvironment environment,
        IOptions<AuthOptions> authOptions,
        IOptions<OidcOptions> oidcOptions,
        IUserContext userContext,
        ILocalJwtTokenService localJwtTokenService)
    {
        _authPersistenceService = authPersistenceService;
        _environment = environment;
        _authOptions = authOptions.Value ?? new AuthOptions();
        _oidcOptions = oidcOptions.Value ?? new OidcOptions();
        _userContext = userContext;
        _localJwtTokenService = localJwtTokenService;
    }

    public AuthInfoResult GetAuthInfo()
    {
        var mode = _authOptions.GetNormalizedMode();
        return new AuthInfoResult
        {
            Mode = mode,
            RequirePassword = _authOptions.IsLocalPasswordMode(),
            AllowRegistration = CanRegister(),
            IsDevelopmentEnvironment = _environment.IsDevelopment(),
            Oidc = _authOptions.IsOidcMode()
                ? new OidcInfoResult
                {
                    Authority = _oidcOptions.Authority,
                    Audience = _oidcOptions.Audience,
                    ClientId = _oidcOptions.ClientId,
                    Scopes = _oidcOptions.GetScopeList(),
                    RedirectPath = _oidcOptions.RedirectPath,
                    PostLogoutRedirectPath = _oidcOptions.PostLogoutRedirectPath,
                    RequireHttpsMetadata = _oidcOptions.RequireHttpsMetadata
                }
                : null
        };
    }

    public async Task<AuthServiceResult<CurrentUserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var user = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (user == null)
        {
            return AuthServiceResult<CurrentUserDto>.Failure(401, "AUTH_NOT_LOGGED_IN", "Not logged in.");
        }

        return AuthServiceResult<CurrentUserDto>.Succeeded(MapToCurrentUser(user));
    }

    public async Task<AuthServiceResult<AuthTokenResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var mode = _authOptions.GetNormalizedMode();

        if (_authOptions.IsOidcMode() || _authOptions.IsDisabledMode())
        {
            return BuildAuthModeError<AuthTokenResponseDto>(mode);
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return AuthServiceResult<AuthTokenResponseDto>.Failure(400, "AUTH_USERNAME_REQUIRED", "Username is required.");
        }

        var user = await _authPersistenceService.FindUserByUsernameAsync(username, cancellationToken);
        if (user == null)
        {
            return AuthServiceResult<AuthTokenResponseDto>.Failure(401, "AUTH_INVALID_CREDENTIALS", "Invalid username or password.");
        }

        if (_authOptions.IsLocalPasswordMode())
        {
            var passwordValue = request.Password?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(passwordValue))
            {
                return AuthServiceResult<AuthTokenResponseDto>.Failure(400, "AUTH_PASSWORD_REQUIRED", "Password is required.");
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(passwordValue, user.PasswordHash))
            {
                return AuthServiceResult<AuthTokenResponseDto>.Failure(401, "AUTH_INVALID_CREDENTIALS", "Invalid username or password.");
            }

            return AuthServiceResult<AuthTokenResponseDto>.Succeeded(BuildTokenResponse(user));
        }

        if (_authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment())
        {
            return AuthServiceResult<AuthTokenResponseDto>.Succeeded(BuildTokenResponse(user));
        }

        return BuildAuthModeError<AuthTokenResponseDto>(mode);
    }

    public async Task<AuthServiceResult<AuthTokenResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!CanRegister())
        {
            return AuthServiceResult<AuthTokenResponseDto>.Failure(
                503,
                "AUTH_REGISTRATION_DISABLED",
                "Registration is not available in the current deployment mode.");
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return AuthServiceResult<AuthTokenResponseDto>.Failure(400, "AUTH_USERNAME_REQUIRED", "Username is required.");
        }

        var password = request.Password?.Trim() ?? string.Empty;
        var passwordError = ValidatePasswordStrength(password);
        if (passwordError != null)
        {
            return AuthServiceResult<AuthTokenResponseDto>.Failure(400, "AUTH_WEAK_PASSWORD", passwordError);
        }

        var existing = await _authPersistenceService.UsernameExistsAsync(username, cancellationToken);
        if (existing)
        {
            return AuthServiceResult<AuthTokenResponseDto>.Failure(409, "AUTH_USERNAME_TAKEN", "Username is already taken.");
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

        await _authPersistenceService.CreateUserAsync(user, cancellationToken);

        return AuthServiceResult<AuthTokenResponseDto>.Succeeded(BuildTokenResponse(user), created: true);
    }

    public async Task<AuthServiceResult<bool>> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!_authOptions.IsLocalPasswordMode() && !(_authOptions.IsDevelopmentUsernameMode() && _environment.IsDevelopment()))
        {
            return AuthServiceResult<bool>.Failure(
                503,
                "AUTH_PASSWORD_NOT_AVAILABLE",
                "Password management is not available in the current deployment mode.");
        }

        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return AuthServiceResult<bool>.Failure(401, "AUTH_NOT_LOGGED_IN", "You must be logged in to change your password.");
        }

        var storedUser = await _authPersistenceService.FindUserByIdAsync(currentUser.Id, cancellationToken);
        if (storedUser == null)
        {
            return AuthServiceResult<bool>.Failure(404, "AUTH_USER_NOT_FOUND", "Current user record was not found.");
        }

        var newPassword = request.NewPassword?.Trim() ?? string.Empty;
        var passwordError = ValidatePasswordStrength(newPassword);
        if (passwordError != null)
        {
            return AuthServiceResult<bool>.Failure(400, "AUTH_WEAK_PASSWORD", passwordError);
        }

        var updated = await _authPersistenceService.UpdatePasswordHashAsync(
            storedUser.Id,
            BCrypt.Net.BCrypt.HashPassword(newPassword, _authOptions.BcryptWorkFactor),
            cancellationToken);

        if (!updated)
        {
            return AuthServiceResult<bool>.Failure(404, "AUTH_USER_NOT_FOUND", "Current user record was not found.");
        }

        return AuthServiceResult<bool>.Succeeded(true);
    }

    public AuthServiceResult<string?> ValidateImpersonation(string? role)
    {
        var normalizedRole = role?.Trim()?.ToLowerInvariant();
        var validRoles = new HashSet<string> { "student", "teacher", "admin", "school_leader" };

        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return AuthServiceResult<string?>.Succeeded(null);
        }

        if (!validRoles.Contains(normalizedRole))
        {
            return AuthServiceResult<string?>.Failure(
                400,
                "AUTH_INVALID_IMPERSONATION_ROLE",
                $"Invalid role: '{role}'. Allowed: student, teacher, admin, school_leader.");
        }

        return AuthServiceResult<string?>.Succeeded(normalizedRole);
    }

    public async Task<AuthServiceResult<CurrentUserDto>> JoinClassAsync(JoinClassRequestDto request, CancellationToken cancellationToken = default)
    {
        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return AuthServiceResult<CurrentUserDto>.Failure(401, "AUTH_NOT_LOGGED_IN", "Not logged in.");
        }

        if (string.IsNullOrWhiteSpace(request.TeacherId) && string.IsNullOrWhiteSpace(request.TeacherUsername))
        {
            return AuthServiceResult<CurrentUserDto>.Failure(400, "AUTH_TEACHER_REQUIRED", "TeacherId or TeacherUsername is required.");
        }

        var storedUser = await _authPersistenceService.FindUserByIdAsync(currentUser.Id, cancellationToken);
        if (storedUser == null)
        {
            return AuthServiceResult<CurrentUserDto>.Failure(404, "AUTH_USER_NOT_FOUND", "Current user record was not found.");
        }

        AppUser? teacher = null;
        if (!string.IsNullOrWhiteSpace(request.TeacherId)
            && int.TryParse(request.TeacherId, out var teacherId)
            && teacherId > 0)
        {
            teacher = await _authPersistenceService.FindTeacherByIdAsync(teacherId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.TeacherUsername))
        {
            var teacherUsername = request.TeacherUsername.Trim();
            teacher = await _authPersistenceService.FindTeacherByUsernameAsync(teacherUsername, cancellationToken);
        }

        if (teacher == null)
        {
            return AuthServiceResult<CurrentUserDto>.Failure(404, "AUTH_TEACHER_NOT_FOUND", "Teacher not found.");
        }

        var joinedUser = await _authPersistenceService.JoinClassAsync(
            new JoinClassUpdateCommand(
                storedUser.Id,
                teacher.Id,
                request.RealName,
                request.StudentNumber,
                request.SchoolName,
                request.ClassName),
            cancellationToken);

        if (joinedUser == null)
        {
            return AuthServiceResult<CurrentUserDto>.Failure(404, "AUTH_USER_NOT_FOUND", "Current user record was not found.");
        }

        return AuthServiceResult<CurrentUserDto>.Succeeded(MapToCurrentUser(joinedUser));
    }

    private AuthServiceResult<T> BuildAuthModeError<T>(string mode)
    {
        if (string.Equals(mode, AuthOptions.ModeOidc, StringComparison.OrdinalIgnoreCase))
        {
            return AuthServiceResult<T>.Failure(
                503,
                "AUTH_MODE_OIDC_REQUIRED",
                "当前部署要求统一认证登录，请使用 OIDC 登录入口。");
        }

        if (string.Equals(mode, AuthOptions.ModeDisabled, StringComparison.OrdinalIgnoreCase))
        {
            return AuthServiceResult<T>.Failure(
                503,
                "AUTH_MODE_DISABLED",
                "当前部署未启用登录入口，请联系管理员。");
        }

        return AuthServiceResult<T>.Failure(
            503,
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

public sealed class AuthInfoResult
{
    public string Mode { get; init; } = string.Empty;
    public bool RequirePassword { get; init; }
    public bool AllowRegistration { get; init; }
    public bool IsDevelopmentEnvironment { get; init; }
    public OidcInfoResult? Oidc { get; init; }
}

public sealed class OidcInfoResult
{
    public string? Authority { get; init; }
    public string? Audience { get; init; }
    public string? ClientId { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
    public string? RedirectPath { get; init; }
    public string? PostLogoutRedirectPath { get; init; }
    public bool RequireHttpsMetadata { get; init; }
}

public sealed class AuthServiceResult<T>
{
    public bool Success { get; init; }
    public bool Created { get; init; }
    public int StatusCode { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public T? Value { get; init; }

    public static AuthServiceResult<T> Succeeded(T value, bool created = false) => new()
    {
        Success = true,
        Created = created,
        StatusCode = created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
        Value = value
    };

    public static AuthServiceResult<T> Failure(int statusCode, string errorCode, string message) => new()
    {
        Success = false,
        StatusCode = statusCode,
        ErrorCode = errorCode,
        Message = message
    };
}
