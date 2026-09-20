using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class LocalAdministratorCredentialValidator(AuthenticationProvider authentication)
    : ICredentialValidator
{
    public AuthenticatedPrincipal? Validate(string? token)
    {
        if (!authentication.IsAuthenticated(token))
            return null;

        return new AuthenticatedPrincipal("admin", tenant: null, workspace: null, OperationPermissions.All);
    }
}
