namespace AgentUp.Server.Features.Ports.Services;

public interface IPortAllocationService
{
    /// <summary>Gets or creates a port range base for the workspace (no probing).</summary>
    Task<int> GetBasePortAsync(string workspaceId);

    /// <summary>
    /// Returns a conflict-free port range base for the workspace.
    /// Probes each of the <paramref name="portCount"/> ports starting at the base.
    /// If any is already bound, assigns a fresh range and retries.
    /// </summary>
    Task<int> GetConflictFreeBasePortAsync(string workspaceId, int portCount);

    /// <summary>Releases the workspace's range back to the free pool.</summary>
    Task ReleaseAsync(string workspaceId);
}
