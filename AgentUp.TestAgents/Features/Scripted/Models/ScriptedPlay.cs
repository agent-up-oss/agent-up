using System.Text.Json.Nodes;

namespace AgentUp.TestAgents.Features.Scripted.Models;

/// <summary>
/// One scripted response to a kind of prompt, for a team of agents whose prompts the Server writes:
/// the first play whose every <paramref name="When"/> phrase is in the prompt answers it. It writes
/// files into the session's working directory, makes MCP calls on the servers it was offered, and
/// says <paramref name="Reply"/>.
/// </summary>
public sealed record ScriptedPlay(
    IReadOnlyList<string> When,
    IReadOnlyList<ScriptedFileWrite>? Write = null,
    IReadOnlyList<ScriptedPlayCall>? Mcp = null,
    string? Reply = null);

/// <summary>A file a play writes, relative to the session's working directory.</summary>
public sealed record ScriptedFileWrite(string Path, string Content);

/// <summary>An MCP call a play makes, named as the server was offered.</summary>
public sealed record ScriptedPlayCall(string Server, string Tool, JsonObject? Arguments = null);
