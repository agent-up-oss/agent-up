namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>Opens a page in a workspace's own headless browser, the one its agent drives.</summary>
public interface IReviewBrowser
{
    /// <summary>Navigates the workspace's browser to <paramref name="url"/>; null when it opened, else why not.</summary>
    Task<string?> OpenAsync(string workspaceId, string url, CancellationToken cancellationToken);
}
