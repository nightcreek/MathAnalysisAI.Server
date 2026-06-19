namespace MathAnalysisAI.Server.Services.Auth;

public static class AuthPolicies
{
    public const string AuthenticatedUser = nameof(AuthenticatedUser);
    public const string TeacherOrAdmin = nameof(TeacherOrAdmin);
    public const string AdminOnly = nameof(AdminOnly);
}
