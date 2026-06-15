using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MathAnalysisAI.Server.Services.Auth;

public class CurrentUserService : IUserContext
{
    private const string SessionUserIdKey = "auth_user_id";
    private const string SessionImpersonatedRoleKey = "impersonated_role";

    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;
    private readonly AuthOptions _authOptions;

    public CurrentUserService(
        ApplicationDbContext db,
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment,
        IOptions<AuthOptions> authOptions)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
        _authOptions = authOptions.Value ?? new AuthOptions();
    }

    public async Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var sessionUserId = httpContext.Session.GetInt32(SessionUserIdKey);
        if (sessionUserId.HasValue)
        {
            var sessionUser = await _db.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sessionUserId.Value, cancellationToken);

            if (sessionUser != null)
            {
                var impersonatedRole = GetImpersonatedRole();
                if (!string.IsNullOrWhiteSpace(impersonatedRole)
                    && string.Equals(sessionUser.Role, AppUserRole.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    sessionUser.Role = impersonatedRole;
                }

                return sessionUser;
            }

            httpContext.Session.Remove(SessionUserIdKey);
        }

        if (!ShouldUseDevelopmentFallback())
        {
            return null;
        }

        var fallbackUsername = _authOptions.DevelopmentFallbackUser;
        if (string.IsNullOrWhiteSpace(fallbackUsername))
        {
            return null;
        }

        var fallbackUser = await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == fallbackUsername, cancellationToken);

        if (fallbackUser != null)
        {
            httpContext.Session.SetInt32(SessionUserIdKey, fallbackUser.Id);
        }

        return fallbackUser;
    }

    public string? GetImpersonatedRole()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Session.GetString(SessionImpersonatedRoleKey);
    }

    public void SetImpersonatedRole(string? role)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        if (string.IsNullOrWhiteSpace(role))
        {
            httpContext.Session.Remove(SessionImpersonatedRoleKey);
        }
        else
        {
            httpContext.Session.SetString(SessionImpersonatedRoleKey, role);
        }
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
        if (string.IsNullOrWhiteSpace(currentRole))
        {
            return false;
        }

        return string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase);
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

        return roles.Any(role => !string.IsNullOrWhiteSpace(role)
                                 && string.Equals(role, currentRole, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return user != null;
    }

    private bool ShouldUseDevelopmentFallback()
    {
        return _environment.IsDevelopment()
               && _authOptions.EnableDevelopmentFallback
               && string.Equals(
                   _authOptions.GetNormalizedMode(),
                   AuthOptions.ModeDevelopmentUsername,
                   StringComparison.OrdinalIgnoreCase);
    }
}
