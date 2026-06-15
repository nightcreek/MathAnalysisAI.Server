namespace MathAnalysisAI.Server.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public const string ModeDevelopmentUsername = "DevelopmentUsername";
    public const string ModeLocalPassword = "LocalPassword";
    public const string ModeOidc = "Oidc";
    public const string ModeDisabled = "Disabled";

    public string? Mode { get; set; }
    public bool EnableDevelopmentFallback { get; set; }
    public string? DevelopmentFallbackUser { get; set; }
    public bool EnableDevelopmentMaterialAccessOverride { get; set; }
    public bool EnableDevelopmentSymbolicAccessOverride { get; set; }
    public bool RequireHttps { get; set; }
    public string? CookieSecurePolicy { get; set; }
    public string? CookieSameSite { get; set; }
    public string? CookieName { get; set; }
    public int BcryptWorkFactor { get; set; } = 12;
    public bool AllowRegistration { get; set; }
    public int MinPasswordLength { get; set; } = 6;

    public string GetNormalizedMode()
    {
        return (Mode ?? string.Empty).Trim();
    }

    public bool IsDevelopmentUsernameMode()
    {
        return string.Equals(GetNormalizedMode(), ModeDevelopmentUsername, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsLocalPasswordMode()
    {
        return string.Equals(GetNormalizedMode(), ModeLocalPassword, StringComparison.OrdinalIgnoreCase);
    }
}
