using AgentUp.Browser.Streaming.DTOs;
using AgentUp.Browser.Streaming.Interfaces;

namespace AgentUp.Browser.Streaming;

public sealed class BrowserRemoteSessionService(
    IBrowserRemoteSessionProvider remoteSessions,
    HeadlessBrowserSessionManager manager)
{
    public BrowserRemoteSessionDto GetSession(string workspaceId)
        => remoteSessions.GetSession(workspaceId, manager.GetControlMode(workspaceId));
}
