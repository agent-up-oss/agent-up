using AgentUp.Server.Features.Browser.DTOs;
using AgentUp.Server.Features.Browser.Interfaces;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserRemoteSessionService(
    IBrowserRemoteSessionProvider remoteSessions,
    HeadlessBrowserSessionManager manager)
{
    public BrowserRemoteSessionDto GetSession(string workspaceId)
        => remoteSessions.GetSession(workspaceId, manager.GetControlMode(workspaceId));
}
