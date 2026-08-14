using AgentUp.Browser.Streaming.DTOs;
using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming.Interfaces;

public interface IBrowserRemoteSessionProvider
{
    BrowserRemoteSessionDto GetSession(string workspaceId, BrowserControlMode mode);
}
