using AgentUp.Server.Features.Browser.Models;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserPendingCommandService(
    BrowserSessionStore store,
    BrowserWorkspaceIdParser workspaceIdParser)
{
    public async Task<BrowserPendingCommandResult> GetAsync(
        string? workspaceIds,
        int timeoutMs,
        CancellationToken ct)
    {
        var ids = workspaceIdParser.Parse(workspaceIds);
        if (ids.Count == 0)
            return new BrowserPendingCommandResult(false, null);

        var command = await store.TryDequeueAsync(ids, TimeSpan.FromMilliseconds(timeoutMs), ct);
        return new BrowserPendingCommandResult(true, command);
    }
}
