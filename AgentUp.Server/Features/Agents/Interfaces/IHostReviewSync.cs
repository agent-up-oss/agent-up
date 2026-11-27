namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>
/// Brings host pull requests into the review queue for an idle reviewer. Called on the same
/// cadence the loop waits for work, so a PR that appears after the reviewer was idle is still
/// taken.
/// </summary>
public interface IHostReviewSync
{
    Task SyncAsync(string workspaceId, CancellationToken cancellationToken);
}
