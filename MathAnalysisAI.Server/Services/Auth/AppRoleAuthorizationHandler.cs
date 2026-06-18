using Microsoft.AspNetCore.Authorization;

namespace MathAnalysisAI.Server.Services.Auth;

public sealed class AppRoleAuthorizationHandler : AuthorizationHandler<AppRoleRequirement>
{
    private readonly IUserContext _userContext;

    public AppRoleAuthorizationHandler(IUserContext userContext)
    {
        _userContext = userContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AppRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var role = await _userContext.GetCurrentRoleAsync();
        if (string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        if (requirement.AllowedRoles.Any(allowed =>
                string.Equals(allowed, role, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }
    }
}
