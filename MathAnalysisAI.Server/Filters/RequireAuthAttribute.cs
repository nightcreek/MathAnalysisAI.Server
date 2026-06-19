using MathAnalysisAI.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;

namespace MathAnalysisAI.Server.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAuthAttribute : AuthorizeAttribute
{
    public RequireAuthAttribute()
    {
        Policy = AuthPolicies.AuthenticatedUser;
    }
}

public static class AuthHttpContextExtensions
{
    public static Models.AppUser? GetCurrentUser(this HttpContext httpContext)
    {
        var userContext = httpContext.RequestServices.GetService<IUserContext>();
        return userContext?.GetCurrentUserAsync(httpContext.RequestAborted).GetAwaiter().GetResult();
    }

    public static int? GetCurrentUserId(this HttpContext httpContext)
    {
        return GetCurrentUser(httpContext)?.Id;
    }
}
