using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAgentSubscriptionLoginProvider
{
    Task<AgentSubscriptionLoginResult> LoginAsync(
        AgentKind kind,
        AgentCommand acpCommand,
        string methodId,
        Action<AgentLoginChallengeDto> onChallenge,
        CancellationToken cancellationToken);
}
