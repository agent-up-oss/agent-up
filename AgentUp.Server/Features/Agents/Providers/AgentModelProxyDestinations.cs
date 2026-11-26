using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Services;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Capabilities.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// The origin a sandboxed model request is forwarded to. OpenCode uses the stored endpoint;
/// Claude, Codex, Cursor, and Ollama-compatible hosts are the well-known vendor origins.
/// </summary>
public sealed class AgentModelProxyDestinations(
    CapabilityModulesController modules,
    IConfiguration configuration) : IAgentModelProxyDestinations
{
    public const string ClaudeOrigin = "https://api.anthropic.com";
    public const string CodexOrigin = "https://api.openai.com";
    public const string CursorOrigin = "https://api2.cursor.sh";

    public AgentModelProxyTarget? Resolve(string capability)
    {
        if (capability.Equals(OpencodeAgentCapability.PackageId, StringComparison.OrdinalIgnoreCase))
            return Opencode();
        if (capability.Equals("claude", StringComparison.OrdinalIgnoreCase))
            return new AgentModelProxyTarget(ClaudeOrigin, null);
        if (capability.Equals("codex", StringComparison.OrdinalIgnoreCase))
            return new AgentModelProxyTarget(CodexOrigin, null);
        if (capability.Equals("cursor", StringComparison.OrdinalIgnoreCase))
            return new AgentModelProxyTarget(CursorOrigin, null);
        if (capability.Equals("ollama", StringComparison.OrdinalIgnoreCase))
            return Ollama();
        return null;
    }

    private AgentModelProxyTarget? Opencode()
    {
        var settings = modules.ReadSettings(OpencodeAgentCapability.PackageId);
        if (!settings.TryGetValue(OpencodeAgentCapability.EndpointKey, out var endpoint)
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return null;
        settings.TryGetValue(OpencodeAgentCapability.TokenKey, out var token);
        return new AgentModelProxyTarget(uri.GetLeftPart(UriPartial.Authority), string.IsNullOrWhiteSpace(token) ? null : token);
    }

    private AgentModelProxyTarget Ollama()
    {
        var configured = configuration["AGENTUP_OLLAMA_URL"];
        if (string.IsNullOrWhiteSpace(configured) || !Uri.TryCreate(configured, UriKind.Absolute, out var uri))
            uri = new Uri("http://127.0.0.1:11434");
        return new AgentModelProxyTarget(uri.GetLeftPart(UriPartial.Authority), null);
    }
}
