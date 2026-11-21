using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Scripted.Models;

namespace AgentUp.TestAgents.Features.Scripted.Interfaces;

/// <summary>Calls a tool on an MCP server the agent was offered, over Streamable HTTP.</summary>
public interface IScriptedMcpClient
{
    Task<ScriptedMcpResult> CallAsync(
        ScriptedMcpServer server,
        string tool,
        JsonObject arguments,
        CancellationToken cancellationToken);
}
