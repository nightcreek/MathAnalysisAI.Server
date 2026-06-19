using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MathAnalysisAI.Server.Services.Auth;

public sealed class LocalJwtTokenService : ILocalJwtTokenService
{
    private readonly AuthOptions _authOptions;
    private readonly IWebHostEnvironment _environment;

    public LocalJwtTokenService(IOptions<AuthOptions> authOptions, IWebHostEnvironment environment)
    {
        _authOptions = authOptions.Value ?? new AuthOptions();
        _environment = environment;
    }

    public LocalJwtTokenResult IssueToken(AppUser user)
    {
        var signingKey = ResolveSigningKey();
        var issuer = string.IsNullOrWhiteSpace(_authOptions.TokenIssuer)
            ? "MathAnalysisAI.Server"
            : _authOptions.TokenIssuer.Trim();
        var audience = string.IsNullOrWhiteSpace(_authOptions.TokenAudience)
            ? "MathAnalysisAI.Client"
            : _authOptions.TokenAudience.Trim();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Clamp(_authOptions.AccessTokenLifetimeMinutes, 5, 24 * 60));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("app_user_id", user.Id.ToString()),
            new("app_role", user.Role ?? AppUserRole.Student)
        };

        if (!string.IsNullOrWhiteSpace(user.RealName))
        {
            claims.Add(new Claim("name", user.RealName));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new LocalJwtTokenResult(encoded, expiresAtUtc);
    }

    private string ResolveSigningKey()
    {
        if (!string.IsNullOrWhiteSpace(_authOptions.TokenSigningKey))
        {
            return _authOptions.TokenSigningKey.Trim();
        }

        if (_environment.IsDevelopment())
        {
            return "development-only-signing-key-change-me-please-2026";
        }

        throw new InvalidOperationException("Auth:TokenSigningKey must be configured when issuing local JWT tokens.");
    }
}
