namespace AgentUp.TestAgents.Features.IdentityProvider.Services;

/// <summary>
/// Runs the identity provider for the lifetime of the process.
/// <para>
/// The listener's lifetime and the cancellation that ends it live here rather than in the slice's
/// controller, which stays at routing complexity.
/// </para>
/// </summary>
public sealed class IdentityProviderHostService
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
            // Asked to stop; the provider is disposed on the way out.
            return;
        }
    }
}
