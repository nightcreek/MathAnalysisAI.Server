using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Auth;

public class CurrentUserService : IUserContext
{
    private readonly IIdentityKernel _identityKernel;

    public CurrentUserService(IIdentityKernel identityKernel)
    {
        _identityKernel = identityKernel;
    }

    public async Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        => await _identityKernel.GetCurrentUserAsync(cancellationToken);

    public string? GetImpersonatedRole()
        => _identityKernel.GetImpersonatedRole();

    public void SetImpersonatedRole(string? role)
        => _identityKernel.SetImpersonatedRole(role);

    public async Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
        => await _identityKernel.GetCurrentUserIdAsync(cancellationToken);

    public async Task<string?> GetCurrentRoleAsync(CancellationToken cancellationToken = default)
        => await _identityKernel.GetCurrentRoleAsync(cancellationToken);

    public async Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default)
        => await _identityKernel.IsInRoleAsync(role, cancellationToken);

    public async Task<bool> IsInAnyRoleAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default)
        => await _identityKernel.IsInAnyRoleAsync(roles, cancellationToken);

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
        => await _identityKernel.IsAuthenticatedAsync(cancellationToken);
}
