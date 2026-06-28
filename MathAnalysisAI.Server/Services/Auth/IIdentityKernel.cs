using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Auth;

public interface IIdentityKernel
{
    Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    string? GetImpersonatedRole();
    void SetImpersonatedRole(string? role);
    Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
    Task<string?> GetCurrentRoleAsync(CancellationToken cancellationToken = default);
    Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<bool> IsInAnyRoleAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default);
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
    Task<IdentityAuthenticationResult> AuthenticateAsync(string username, string? password, CancellationToken cancellationToken = default);
    Task<bool> CanViewRealStudentInfoAsync(int viewerUserId, int targetStudentUserId, int courseId, CancellationToken cancellationToken = default);
    Task<bool> CanViewCourseLeaderboardWithRealNamesAsync(int viewerUserId, int courseId, CancellationToken cancellationToken = default);
}

public sealed record IdentityAuthenticationResult(
    bool Succeeded,
    AppUser? User,
    int StatusCode,
    string? ErrorCode,
    string? Message)
{
    public static IdentityAuthenticationResult Success(AppUser user) =>
        new(true, user, StatusCodes.Status200OK, null, null);

    public static IdentityAuthenticationResult Failure(int statusCode, string errorCode, string message) =>
        new(false, null, statusCode, errorCode, message);
}
