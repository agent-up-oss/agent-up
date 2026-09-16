using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentProcessEnvironmentProvider(
    AgentCliHomeProvider home,
    IAgentClaudeCredentialStore credentials) : IAgentProcessEnvironmentProvider
{
    public IReadOnlyDictionary<string, string> EnvironmentFor(AgentKind kind)
    {
        home.Ensure();
        // HOME is redirected so every agent CLI keeps its credentials inside the Server data
        // directory rather than in the service account's real home.
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = home.HomePath
        };
        if (kind == AgentKind.Claude)
        {
            var token = credentials.Read();
            if (!string.IsNullOrWhiteSpace(token))
                environment["CLAUDE_CODE_OAUTH_TOKEN"] = token;
        }

        return environment;
    }
}
