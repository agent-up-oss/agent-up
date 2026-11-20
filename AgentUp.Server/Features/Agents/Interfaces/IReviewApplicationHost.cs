namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>
/// Runs the revision a product reviewer was given. A product reviewer may operate the application
/// but not start or stop its workspace, so the Server does that around its turn, the way it does
/// the Git work around every member's turn.
/// </summary>
public interface IReviewApplicationHost
{
    /// <summary>
    /// Restarts the workspace on what is checked out and returns where its HTTP applications
    /// answer, or the reason it could not start.
    /// </summary>
    Task<(IReadOnlyList<string> Addresses, string? Error)> RunAsync(string workspaceId, CancellationToken cancellationToken);
}
