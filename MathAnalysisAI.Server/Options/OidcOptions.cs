namespace MathAnalysisAI.Server.Options;

public sealed class OidcOptions
{
    public const string SectionName = "Oidc";

    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public string? ClientId { get; set; }
    public string? Scopes { get; set; }
    public string? RoleClaimType { get; set; }
    public string? NameClaimType { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public string RedirectPath { get; set; } = "/login-callback.html";
    public string PostLogoutRedirectPath { get; set; } = "/login.html";

    public IReadOnlyList<string> GetScopeList()
    {
        return (Scopes ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
