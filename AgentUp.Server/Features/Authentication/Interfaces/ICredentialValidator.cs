using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Interfaces;

public interface ICredentialValidator
{
    AuthenticatedPrincipal? Validate(string? token);
}
