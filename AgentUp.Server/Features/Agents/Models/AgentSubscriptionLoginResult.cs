using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Models;

public sealed record AgentSubscriptionLoginResult(
    bool Succeeded,
    string? Error,
    string? ClaudeOAuthToken,
    AgentLoginChallengeDto Challenge)
{
    public static AgentSubscriptionLoginResult Failed(string error, AgentLoginChallengeDto? challenge = null) =>
        new(false, error, null, challenge ?? new AgentLoginChallengeDto(null, null, null));

    public static AgentSubscriptionLoginResult SucceededResult(AgentLoginChallengeDto challenge, string? claudeOAuthToken) =>
        new(true, null, claudeOAuthToken, challenge);
}
