using AgentUp.TestAgents.Features.IdentityProvider.Services;

namespace AgentUp.TestAgents.Features.IdentityProvider.Controllers;

/// <summary>The identity provider slice's boundary: running the provider until it is stopped.</summary>
public sealed class IdentityProviderController
{
    /// <summary>
    /// Starts the provider and reports the port it bound, so a harness that asked for an ephemeral
    /// one can read back what it actually got.
    /// </summary>
    public async Task ServeAsync(int port, string? publicOrigin, Action<int> onListening, CancellationToken cancellationToken)
    {
        await using var provider = new TestIdentityProviderService(port, publicOrigin);
        provider.Start();
        onListening(provider.Port);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
}
