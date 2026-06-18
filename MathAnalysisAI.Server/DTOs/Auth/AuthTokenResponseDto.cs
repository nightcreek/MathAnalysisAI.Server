namespace MathAnalysisAI.Server.DTOs.Auth;

public sealed class AuthTokenResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
    public CurrentUserDto User { get; init; } = new();
}
