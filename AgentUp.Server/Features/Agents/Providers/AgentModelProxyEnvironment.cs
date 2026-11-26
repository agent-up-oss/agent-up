using System.Text.Json.Nodes;
using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Providers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Points a sandboxed agent's model HTTP at the Server's loopback proxy so the process does
/// not need a route to the vendor or to a configured Ollama-compatible host.
/// </summary>
public static class AgentModelProxyEnvironment
{
    public const string KeyVariable = "AGENT_UP_MODEL_PROXY_KEY";

    public static IReadOnlyDictionary<string, string> Apply(
        IReadOnlyDictionary<string, string> environment,
        string loopbackBaseUrl,
        string key)
    {
        var rewritten = new Dictionary<string, string>(environment, StringComparer.Ordinal);
        var prefix = $"{loopbackBaseUrl.TrimEnd('/')}/api/agent-model/{key}";
        rewritten[KeyVariable] = key;
        rewritten["ANTHROPIC_BASE_URL"] = prefix + "/claude";
        rewritten["OPENAI_BASE_URL"] = prefix + "/codex/v1";
        rewritten["OPENAI_API_BASE"] = prefix + "/codex/v1";
        rewritten["CURSOR_API_BASE_URL"] = prefix + "/cursor";
        rewritten["OLLAMA_HOST"] = prefix + "/ollama";
        if (rewritten.TryGetValue(OpencodeConfigProvider.ConfigVariable, out var config))
            rewritten[OpencodeConfigProvider.ConfigVariable] = RewriteOpencode(config, prefix + "/opencode");
        rewritten[OpencodeConfigProvider.TokenVariable] = key;
        return rewritten;
    }

    internal static string RewriteOpencode(string config, string prefix)
    {
        try
        {
            if (JsonNode.Parse(config) is not JsonObject root
                || root["provider"] is not JsonObject providers
                || providers[OpencodeConfigProvider.ProviderId] is not JsonObject provider)
                return config;
            var options = provider["options"] as JsonObject ?? new JsonObject();
            provider["options"] = options;
            options["baseURL"] = ProxiedBaseUrl(options["baseURL"]?.GetValue<string>(), prefix);
            options["apiKey"] = "{env:" + OpencodeConfigProvider.TokenVariable + "}";
            return root.ToJsonString();
        }
        catch (System.Text.Json.JsonException)
        {
            return config;
        }
    }

    /// <summary>
    /// Keep the original path (usually <c>/v1</c>) so the proxy can append it to the real origin.
    /// </summary>
    internal static string ProxiedBaseUrl(string? current, string prefix)
    {
        if (current is not null
            && Uri.TryCreate(current, UriKind.Absolute, out var uri)
            && uri.AbsolutePath is not "" and not "/")
            return prefix + uri.AbsolutePath.TrimEnd('/');
        return prefix + "/v1";
    }
}
