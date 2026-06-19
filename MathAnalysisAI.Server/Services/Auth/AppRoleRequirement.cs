using Microsoft.AspNetCore.Authorization;

namespace MathAnalysisAI.Server.Services.Auth;

public sealed class AppRoleRequirement : IAuthorizationRequirement
{
    public AppRoleRequirement(params string[] allowedRoles)
    {
        AllowedRoles = allowedRoles ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> AllowedRoles { get; }
}
