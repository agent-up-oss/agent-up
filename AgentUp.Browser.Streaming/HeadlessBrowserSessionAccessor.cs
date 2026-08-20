using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming;

public sealed class HeadlessBrowserSessionAccessor(HeadlessBrowserSessionManager? manager)
{
    public BrowserSessionState? GetSession(string workspaceId) => manager?.GetSession(workspaceId);
}
