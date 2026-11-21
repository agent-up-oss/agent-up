using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// The Agent-Up MCP servers an ACP agent is offered when its session starts. The agent runs on
/// the Server's host, so every endpoint is addressed over loopback, where MCP is anonymous by
/// design; nothing is offered to an agent that does not advertise HTTP MCP support.
/// </summary>
public sealed class AgentMcpServerProvider(IConfiguration configuration, IServer? server = null)
{
    /// <summary>The named endpoints a coding agent works through, in the order they are offered.</summary>
    public static readonly IReadOnlyList<string> Endpoints =
        ["tasks", "orchestration", "commits", "verification", "browser", "audit", "training"];

    private const string Fallback = "http://127.0.0.1:5000";

    /// <summary>Whether the agent's <c>initialize</c> result says it can reach MCP servers over HTTP.</summary>
    public static bool SupportsHttp(JsonElement initialized) =>
        initialized.ValueKind == JsonValueKind.Object
        && initialized.TryGetProperty("agentCapabilities", out var capabilities)
        && capabilities.ValueKind == JsonValueKind.Object
        && capabilities.TryGetProperty("mcpCapabilities", out var mcp)
        && mcp.ValueKind == JsonValueKind.Object
        && mcp.TryGetProperty("http", out var http)
        && http.ValueKind == JsonValueKind.True;

    /// <summary>
    /// The header that carries an agent session's credential. Spelled here rather than read from
    /// the Authentication slice so the Agents slice does not depend on its internals; the two are
    /// held equal by a test.
    /// </summary>
    public const string SessionHeader = "X-Agent-Up-Agent-Session";

    /// <summary>
    /// The <c>mcpServers</c> value for <c>session/new</c> and <c>session/load</c>. With a session
    /// credential every server carries it, so the endpoints pin the agent to its own workspace and
    /// narrow it to that workspace's team member grant.
    /// </summary>
    public IReadOnlyList<object> Servers(bool httpSupported, string? sessionToken = null)
    {
        if (!httpSupported)
            return [];
        object[] headers = string.IsNullOrEmpty(sessionToken)
            ? []
            : [new { name = SessionHeader, value = sessionToken }];
        var baseUrl = LoopbackBaseUrl();
        return Endpoints
            .Select(name => (object)new
            {
                type = "http",
                name = $"agent-up-{name}",
                url = $"{baseUrl}/mcp/{name}",
                headers
            })
            .ToArray();
    }

    /// <summary>
    /// The address the Server answers on, as seen from its own host: the bound address when the
    /// host knows it, else the configured URL, with a wildcard host turned into loopback.
    /// </summary>
    public string LoopbackBaseUrl()
    {
        var bound = server?.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        var configured = (configuration["urls"] ?? configuration["ASPNETCORE_URLS"] ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidates = bound.Concat(configured).ToArray();
        var chosen = candidates.FirstOrDefault(address => address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                     ?? candidates.FirstOrDefault()
                     ?? Fallback;
        return Loopback(chosen);
    }

    private static string Loopback(string address)
    {
        var normalized = address.Replace("://*", "://127.0.0.1", StringComparison.Ordinal)
            .Replace("://+", "://127.0.0.1", StringComparison.Ordinal);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return Fallback;
        var host = uri.Host is "0.0.0.0" or "[::]" or "::" ? "127.0.0.1" : uri.Host;
        return $"{uri.Scheme}://{(host.Contains(':') ? $"[{host}]" : host)}:{uri.Port}";
    }
}
