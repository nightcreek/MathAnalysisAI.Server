using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using Microsoft.Extensions.Options;

namespace MathAnalysisAI.Server.Services.Auth;

public class CurrentUserService : IUserContext
{
    private const string CurrentUserItemKey = "CurrentUser";
    private const string ImpersonatedRoleItemKey = "impersonated_role";

    private readonly ICurrentUserPersistenceService _currentUserPersistenceService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthOptions _authOptions;
    private readonly OidcOptions _oidcOptions;
    private readonly ILogger<CurrentUserService> _logger;

    public CurrentUserService(
        ICurrentUserPersistenceService currentUserPersistenceService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AuthOptions> authOptions,
        IOptions<OidcOptions> oidcOptions,
        ILogger<CurrentUserService> logger)
    {
        _currentUserPersistenceService = currentUserPersistenceService;
        _httpContextAccessor = httpContextAccessor;
        _authOptions = authOptions.Value ?? new AuthOptions();
        _oidcOptions = oidcOptions.Value ?? new OidcOptions();
        _logger = logger;
    }

    public async Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return await ResolveDevelopmentFallbackUserAsync(cancellationToken);
        }

        if (httpContext.Items.TryGetValue(CurrentUserItemKey, out var cached)
            && cached is AppUser cachedUser)
        {
            return cachedUser;
        }

        var user = await ResolveCurrentUserAsync(httpContext.User, cancellationToken);
        if (user == null)
        {
            return null;
        }

        var impersonatedRole = GetImpersonatedRole();
        if (!string.IsNullOrWhiteSpace(impersonatedRole)
            && string.Equals(user.Role, AppUserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            user = CloneWithRole(user, impersonatedRole);
        }

        httpContext.Items[CurrentUserItemKey] = user;
        return user;
    }

    public string? GetImpersonatedRole()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Items[ImpersonatedRoleItemKey] as string;
    }

    public void SetImpersonatedRole(string? role)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            httpContext.Items.Remove(ImpersonatedRoleItemKey);
            return;
        }

        httpContext.Items[ImpersonatedRoleItemKey] = role.Trim();
    }

    public async Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return user?.Id;
    }

    public async Task<string?> GetCurrentRoleAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return user?.Role;
    }

    public async Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var currentRole = await GetCurrentRoleAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(currentRole)
               && string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsInAnyRoleAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        if (roles == null)
        {
            return false;
        }

        var currentRole = await GetCurrentRoleAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(currentRole))
        {
            return false;
        }

        return roles.Any(role =>
            !string.IsNullOrWhiteSpace(role)
            && string.Equals(role, currentRole, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return user != null;
    }

    private async Task<AppUser?> ResolveCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var appUserIdClaim = principal.FindFirstValue("app_user_id");
        if (int.TryParse(appUserIdClaim, out var appUserId) && appUserId > 0)
        {
            return await _currentUserPersistenceService.FindUserByIdAsync(appUserId, cancellationToken);
        }

        var username = ResolveUsername(principal);
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var user = await _currentUserPersistenceService.FindUserByUsernameAsync(username, cancellationToken);

        if (user != null || !_authOptions.IsOidcMode())
        {
            return user;
        }

        return await _currentUserPersistenceService.ProvisionOidcUserAsync(principal, username, cancellationToken);
    }

    private async Task<AppUser?> ResolveDevelopmentFallbackUserAsync(CancellationToken cancellationToken)
    {
        if (!_authOptions.EnableDevelopmentFallback || !_authOptions.IsDevelopmentUsernameMode())
        {
            return null;
        }

        var fallbackUsername = _authOptions.DevelopmentFallbackUser?.Trim();
        if (string.IsNullOrWhiteSpace(fallbackUsername))
        {
            return null;
        }

        return await _currentUserPersistenceService.FindUserByUsernameAsync(fallbackUsername, cancellationToken);
    }

    private string? ResolveUsername(ClaimsPrincipal principal)
    {
        var preferredNameClaim = _oidcOptions.NameClaimType;
        var candidates = new[]
        {
            preferredNameClaim,
            ClaimTypes.Name,
            JwtRegisteredClaimNames.UniqueName,
            "preferred_username",
            "email",
            ClaimTypes.Email,
            ClaimTypes.Upn,
            JwtRegisteredClaimNames.Sub
        };

        foreach (var claimType in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var value = principal.FindFirstValue(claimType!);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return principal.Identity?.Name?.Trim();
    }

    private static AppUser CloneWithRole(AppUser user, string role)
    {
        return new AppUser
        {
            Id = user.Id,
            Username = user.Username,
            PasswordHash = user.PasswordHash,
            RealName = user.RealName,
            StudentNumber = user.StudentNumber,
            Role = role.Trim(),
            SchoolName = user.SchoolName,
            DepartmentName = user.DepartmentName,
            ClassName = user.ClassName,
            TeacherId = user.TeacherId,
            CreatedAt = user.CreatedAt
        };
    }

}
