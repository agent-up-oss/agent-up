using System.Text.Json;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>JSON-RPC MCP over HTTP to this Server's own loopback MCP endpoints.</summary>
public interface ILoopbackMcpClient
{
    Task<IReadOnlyList<JsonElement>> ListToolsAsync(string endpoint, CancellationToken cancellationToken);

    Task<JsonElement> CallToolAsync(
        string endpoint,
        string tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken);
}
