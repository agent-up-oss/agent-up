using AgentUp.Server.Features.Browser.Models;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class HeadlessBrowserSessionAccessor(HeadlessBrowserSessionManager? manager)
{
    public BrowserSessionState? GetSession(string workspaceId) => manager?.GetSession(workspaceId);
}
