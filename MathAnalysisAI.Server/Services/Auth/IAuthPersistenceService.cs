using MathAnalysisAI.Server.Models;
using System.Security.Claims;

namespace MathAnalysisAI.Server.Services.Auth;

public interface IAuthPersistenceService
{
    Task<AppUser?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<AppUser?> FindUserByIdAsync(int userId, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    Task<AppUser> CreateUserAsync(AppUser user, CancellationToken cancellationToken);
    Task<bool> UpdatePasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken);
    Task<AppUser?> FindTeacherByIdAsync(int teacherId, CancellationToken cancellationToken);
    Task<AppUser?> FindTeacherByUsernameAsync(string teacherUsername, CancellationToken cancellationToken);
    Task<AppUser?> JoinClassAsync(JoinClassUpdateCommand command, CancellationToken cancellationToken);
}

public interface ICurrentUserPersistenceService
{
    Task<AppUser?> FindUserByIdAsync(int userId, CancellationToken cancellationToken);
    Task<AppUser?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<AppUser?> ProvisionOidcUserAsync(ClaimsPrincipal principal, string username, CancellationToken cancellationToken);
}

public interface IPermissionPersistenceService
{
    Task<AppUser?> FindUserByIdAsync(int userId, CancellationToken cancellationToken);
}

public sealed record JoinClassUpdateCommand(
    int UserId,
    int TeacherId,
    string? RealName,
    string? StudentNumber,
    string? SchoolName,
    string? ClassName);
