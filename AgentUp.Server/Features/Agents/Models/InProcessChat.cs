using System.Text.Json;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>One message in an in-process agent's conversation with its chat model.</summary>
public sealed record InProcessChatMessage(
    string Role,
    string Content,
    IReadOnlyList<InProcessToolCall>? ToolCalls = null,
    string? ToolName = null,
    IReadOnlyList<string>? Images = null);

public sealed record InProcessToolCall(string Name, JsonElement Arguments);

/// <summary>A tool offered to the chat model, already narrowed to the team member's grant.</summary>
public sealed record InProcessTool(string Name, string Description, JsonElement Parameters);

/// <summary>What a tool call produced: text for the model, plus any base64 images it captured.</summary>
public sealed record InProcessToolResult(string Text, IReadOnlyList<string> Images, bool IsError, bool CompletesTask = false);

public sealed record InProcessChatSettings(Uri Endpoint, string Model);
