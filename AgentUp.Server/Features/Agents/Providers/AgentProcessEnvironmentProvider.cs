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
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = home.HomePath,
            ["AGENT_CLI_CREDENTIAL_STORE"] = "file"
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
