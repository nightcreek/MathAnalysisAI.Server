using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Auth;

public interface ILocalJwtTokenService
{
    LocalJwtTokenResult IssueToken(AppUser user);
}

public sealed record LocalJwtTokenResult(string AccessToken, DateTime ExpiresAtUtc);
