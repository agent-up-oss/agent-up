using AgentUp.TestAgents.Features.IdentityProvider.Services;

namespace AgentUp.TestAgents.Features.IdentityProvider.Controllers;

/// <summary>The identity provider slice's boundary: running the provider until it is stopped.</summary>
public sealed class IdentityProviderController(IdentityProviderHostService host)
{
    public Task ServeAsync(int port, string? publicOrigin, Action<int> onListening, CancellationToken cancellationToken) =>
        host.ServeAsync(port, publicOrigin, onListening, cancellationToken);
}
