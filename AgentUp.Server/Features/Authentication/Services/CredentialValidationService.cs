using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Models;
using AgentUp.Server.Features.Authentication.Providers;

namespace AgentUp.Server.Features.Authentication.Services;

public sealed class CredentialValidationService(
    AuthenticationModeProvider modes,
    LocalAdministratorCredentialValidator local,
    ExternalBearerCredentialValidator external)
{
    public AuthenticatedPrincipal? Validate(string? token)
    {
        if (modes.Current == AuthenticationMode.Disabled)
            return new AuthenticatedPrincipal("local", tenant: null, workspace: null, OperationPermissions.All);

        ICredentialValidator validator = modes.Current == AuthenticationMode.ExternalBearer ? external : local;
        return validator.Validate(token);
    }
}
