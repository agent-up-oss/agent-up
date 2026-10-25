using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentProcessEnvironmentProvider(
    AgentCliHomeProvider home,
    IAgentClaudeCredentialStore credentials,
    IAgentRuntimePathProvider? runtime = null) : IAgentProcessEnvironmentProvider
{
    public IReadOnlyDictionary<string, string> EnvironmentFor(string agent)
    {
        home.Ensure();
        // HOME is redirected so every agent CLI keeps its credentials inside the Server data
        // directory rather than in the service account's real home.
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = home.HomePath
        };
        var prefixes = runtime?.PathPrefixes() ?? [];
        if (prefixes.Count > 0)
        {
            var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            environment["PATH"] = string.Join(Path.PathSeparator, prefixes.Append(current));
        }

        var runtimeRoot = runtime?.RuntimeRoot();
        if (!string.IsNullOrWhiteSpace(runtimeRoot))
            environment["AGENT_UP_DEV_ROOT"] = runtimeRoot;

        if (agent.Equals("claude", StringComparison.OrdinalIgnoreCase))
        {
            var token = credentials.Read();
            if (!string.IsNullOrWhiteSpace(token))
                environment["CLAUDE_CODE_OAUTH_TOKEN"] = token;
        }

        return environment;
    }
}
