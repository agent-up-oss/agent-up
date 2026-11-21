using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Browser.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>Opens the page through the Browser slice's own navigate boundary, in the workspace's profile.</summary>
public sealed class WorkspaceReviewBrowserProvider(BrowserMcpTools browser) : IReviewBrowser
{
    public async Task<string?> OpenAsync(string workspaceId, string url, CancellationToken cancellationToken)
    {
        var opened = await browser.Navigate(workspaceId, url, cancellationToken);
        return opened.Succeeded ? null : opened.Message;
    }
}
