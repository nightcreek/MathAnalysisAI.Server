using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MathAnalysisAI.Server.Services.Auth;

public sealed class AuthPersistenceService :
    IAuthPersistenceService,
    ICurrentUserPersistenceService,
    IPermissionPersistenceService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AuthPersistenceService> _logger;

    public AuthPersistenceService(
        ApplicationDbContext db,
        ILogger<AuthPersistenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AppUser?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    public async Task<AppUser?> FindUserByIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Username == username, cancellationToken);
    }

    public async Task<AppUser> CreateUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> UpdatePasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        user.PasswordHash = passwordHash;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AppUser?> FindTeacherByIdAsync(int teacherId, CancellationToken cancellationToken)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == teacherId
                    && (x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin),
                cancellationToken);
    }

    public async Task<AppUser?> FindTeacherByUsernameAsync(string teacherUsername, CancellationToken cancellationToken)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Username == teacherUsername
                    && (x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin),
                cancellationToken);
    }

    public async Task<AppUser?> JoinClassAsync(JoinClassUpdateCommand command, CancellationToken cancellationToken)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == command.UserId, cancellationToken);
        if (user == null)
        {
            return null;
        }

        user.TeacherId = command.TeacherId;

        if (!string.IsNullOrWhiteSpace(command.RealName))
        {
            user.RealName = command.RealName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(command.StudentNumber))
        {
            user.StudentNumber = command.StudentNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(command.SchoolName))
        {
            user.SchoolName = command.SchoolName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(command.ClassName))
        {
            user.ClassName = command.ClassName.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.UserId, cancellationToken);
    }

    public async Task<AppUser?> ProvisionOidcUserAsync(ClaimsPrincipal principal, string username, CancellationToken cancellationToken)
    {
        var normalizedUsername = Truncate(username.Trim(), 64);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return null;
        }

        var realName = ResolveRealName(principal);
        var user = new AppUser
        {
            Username = normalizedUsername,
            RealName = Truncate(realName, 64),
            Role = AppUserRole.Student,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _db.AppUsers.Add(user);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Provisioned new OIDC user. Username={Username}, Role={Role}",
                user.Username,
                user.Role);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "OIDC user provisioning raced or failed for username {Username}. Reloading existing user.", normalizedUsername);
        }

        return await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);
    }

    private static string? ResolveRealName(ClaimsPrincipal principal)
    {
        var candidates = new[]
        {
            "name",
            ClaimTypes.GivenName,
            ClaimTypes.Name
        };

        foreach (var claimType in candidates)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
