using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class LocalAdministratorCredentialValidator(AuthenticationProvider authentication)
    : ICredentialValidator
{
    public Task<AuthenticatedPrincipal?> ValidateAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (!authentication.IsAuthenticated(token))
            return Task.FromResult<AuthenticatedPrincipal?>(null);

        return Task.FromResult<AuthenticatedPrincipal?>(
            new AuthenticatedPrincipal("admin", tenant: null, workspace: null, OperationPermissions.All));
    }
}
