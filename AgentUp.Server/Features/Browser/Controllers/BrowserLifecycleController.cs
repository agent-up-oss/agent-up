using AgentUp.Server.Features.Browser.Services;

namespace AgentUp.Server.Features.Browser.Controllers;

public sealed class BrowserLifecycleController(
    HeadlessBrowserSessionManager sessions,
    BrowserRemoteDisplayService display)
{
    public Task DisposeSessionAsync(string workspaceId)
        => sessions.DisposeSessionAsync(workspaceId);

    public Task DisconnectAllAsync(string workspaceId, CancellationToken ct)
        => display.DisconnectAllAsync(workspaceId, ct);
}
