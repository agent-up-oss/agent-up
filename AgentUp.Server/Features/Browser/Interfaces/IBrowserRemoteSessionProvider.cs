using AgentUp.Server.Features.Browser.DTOs;
using AgentUp.Server.Features.Browser.Models;

namespace AgentUp.Server.Features.Browser.Interfaces;

public interface IBrowserRemoteSessionProvider
{
    BrowserRemoteSessionDto GetSession(string workspaceId, BrowserControlMode mode);
}
