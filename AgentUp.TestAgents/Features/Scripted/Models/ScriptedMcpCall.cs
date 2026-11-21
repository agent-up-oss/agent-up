using System.Text.Json.Nodes;

namespace AgentUp.TestAgents.Features.Scripted.Models;

/// <summary>An MCP server the agent was offered in <c>session/new</c> or <c>session/load</c>.</summary>
public sealed record ScriptedMcpServer(string Name, string Url, IReadOnlyDictionary<string, string> Headers);

/// <summary>One tool call a prompt directs the scripted agent to make.</summary>
public sealed record ScriptedMcpCall(string Server, string Tool, JsonObject Arguments);

/// <summary>What a tool call returned: the text a person would read, and whether it failed.</summary>
public sealed record ScriptedMcpResult(bool IsError, string Text);
