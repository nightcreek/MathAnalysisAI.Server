using MathAnalysisAI.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MathAnalysisAI.Server.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireAuthAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userContext = context.HttpContext.RequestServices.GetRequiredService<IUserContext>();
        var user = await userContext.GetCurrentUserAsync(context.HttpContext.RequestAborted);

        if (user == null)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "Not logged in.",
                errorCode = "auth_not_logged_in",
                isRetryable = false
            });
            return;
        }

        context.HttpContext.Items["CurrentUser"] = user;
        await next();
    }
}

public static class AuthHttpContextExtensions
{
    private const string CurrentUserKey = "CurrentUser";

    public static Models.AppUser? GetCurrentUser(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(CurrentUserKey, out var item) && item is Models.AppUser user)
        {
            return user;
        }

        var userContext = httpContext.RequestServices.GetService<MathAnalysisAI.Server.Services.Auth.IUserContext>();
        if (userContext != null)
        {
            return userContext.GetCurrentUserAsync().GetAwaiter().GetResult();
        }

        return null;
    }

    public static int? GetCurrentUserId(this HttpContext httpContext)
    {
        return GetCurrentUser(httpContext)?.Id;
    }
}
