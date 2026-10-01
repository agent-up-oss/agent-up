using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Interfaces;

public interface ICredentialValidator
{
    Task<AuthenticatedPrincipal?> ValidateAsync(string? token, CancellationToken cancellationToken = default);
}
