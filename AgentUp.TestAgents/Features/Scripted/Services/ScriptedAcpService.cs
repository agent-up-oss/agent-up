using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Scripted.Interfaces;
using AgentUp.TestAgents.Features.Scripted.Models;
using AgentUp.TestAgents.Features.Scripted.Providers;

namespace AgentUp.TestAgents.Features.Scripted.Services;

/// <summary>
/// A deterministic ACP v1 agent that emits a declared sequence of protocol turns.
/// session/new is immediate; there is no sign-in. With an MCP client it also advertises HTTP MCP,
/// keeps the servers it is offered, and answers a prompt that is an MCP directive by making those
/// tool calls for real and reporting them as ACP tool calls.
/// </summary>
public sealed class ScriptedAcpService(IScriptedTurnScript scripts, IScriptedMcpClient? mcp = null)
{
    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        var script = scripts.Load();
        IReadOnlyList<ScriptedMcpServer> servers = [];
        var remaining = new Queue<ScriptedTurn>(script.Turns);
        var sessionId = $"scripted-{Guid.NewGuid():N}";
        var nextRequestId = 10_000;

        while (await input.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonNode? frame;
            try
            {
                frame = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            if (frame is null)
                continue;

            var method = frame["method"]?.GetValue<string>();
            var id = frame["id"];
            if (method is null)
                continue;

            if (method is "session/new" or "session/load")
                servers = ScriptedMcpDirectiveProvider.Servers(frame["params"]);

            var directive = method == "session/prompt" && mcp is not null
                ? ScriptedMcpDirectiveProvider.Parse(ScriptedMcpDirectiveProvider.PromptText(frame["params"]))
                : null;
            if (directive is not null)
            {
                await CallToolsAsync(directive, servers, sessionId, id, output, cancellationToken);
                continue;
            }

            if (method == "session/prompt")
            {
                var turn = remaining.Count == 0
                    ? new ScriptedTurn(ScriptedTurnKind.Complete)
                    : remaining.Dequeue();
                await PlayAsync(turn, sessionId, id, output, () => nextRequestId++, cancellationToken);
                continue;
            }

            var response = method switch
            {
                "initialize" => Result(id, Initialize(mcp is not null)),
                "session/new" => Result(id, new JsonObject { ["sessionId"] = sessionId }),
                "session/load" => Result(id, new JsonObject { ["sessionId"] = sessionId }),
                "session/cancel" => null,
                _ => Result(id, new JsonObject())
            };
            if (response is null)
                continue;
            await WriteAsync(output, response.ToJsonString(), cancellationToken);
        }
    }

    private static async Task PlayAsync(
        ScriptedTurn turn,
        string sessionId,
        JsonNode? promptId,
        TextWriter output,
        Func<int> nextId,
        CancellationToken cancellationToken)
    {
        switch (turn.Kind)
        {
            case ScriptedTurnKind.Complete:
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn", ["completed"] = true }).ToJsonString(), cancellationToken);
                return;
            case ScriptedTurnKind.EndTurn:
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn" }).ToJsonString(), cancellationToken);
                return;
            case ScriptedTurnKind.RateLimited:
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "rate_limited" }).ToJsonString(), cancellationToken);
                return;
            case ScriptedTurnKind.Crash:
                throw new InvalidOperationException("scripted crash");
            case ScriptedTurnKind.Empty:
                return;
            case ScriptedTurnKind.Whitespace:
                await output.WriteLineAsync("   ");
                await output.FlushAsync(cancellationToken);
                return;
            case ScriptedTurnKind.TruncatedJson:
                await output.WriteAsync("{\"jsonrpc\":\"2.0\",\"id\":");
                await output.FlushAsync(cancellationToken);
                return;
            case ScriptedTurnKind.HoldPipe:
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return;
            case ScriptedTurnKind.InvalidUtf8:
                await output.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"sessionUpdate\":\"tool_call\",\"title\":\"\uD800\"}}");
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn" }).ToJsonString(), cancellationToken);
                return;
            case ScriptedTurnKind.Permission:
                await RequestPermissionAsync(turn, sessionId, promptId, output, nextId, cancellationToken, duplicate: false);
                return;
            case ScriptedTurnKind.DuplicatePermission:
                await RequestPermissionAsync(turn, sessionId, promptId, output, nextId, cancellationToken, duplicate: true);
                return;
            case ScriptedTurnKind.OutOfOrder:
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn" }).ToJsonString(), cancellationToken);
                await RequestPermissionAsync(turn, sessionId, null, output, nextId, cancellationToken, duplicate: false);
                return;
            case ScriptedTurnKind.ToolCall:
            case ScriptedTurnKind.UnknownTool:
                await NotifyToolAsync(output, turn.Name ?? "read_file", cancellationToken);
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn" }).ToJsonString(), cancellationToken);
                return;
            case ScriptedTurnKind.ToolError:
                await NotifyToolAsync(output, turn.Name ?? "read_file", cancellationToken, turn.Error ?? "ENOENT");
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn" }).ToJsonString(), cancellationToken);
                return;
            case ScriptedTurnKind.HugeArgs:
                await NotifyToolAsync(output, turn.Name ?? "write_file", cancellationToken, arguments: new string('x', 3_000_000));
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn" }).ToJsonString(), cancellationToken);
                return;
            default:
                await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn" }).ToJsonString(), cancellationToken);
                return;
        }
    }

    private static async Task RequestPermissionAsync(
        ScriptedTurn turn,
        string sessionId,
        JsonNode? promptId,
        TextWriter output,
        Func<int> nextId,
        CancellationToken cancellationToken,
        bool duplicate)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = nextId(),
            ["method"] = "session/request_permission",
            ["params"] = new JsonObject
            {
                ["sessionId"] = sessionId,
                ["options"] = new JsonArray
                {
                    new JsonObject { ["optionId"] = "allow-once", ["kind"] = "allow_once" },
                    new JsonObject { ["optionId"] = "reject-once", ["kind"] = "reject_once" }
                },
                ["toolCall"] = new JsonObject
                {
                    ["kind"] = turn.ToolKind ?? "edit",
                    ["title"] = turn.Title ?? "Write file",
                    ["command"] = turn.Command,
                    ["locations"] = new JsonArray { new JsonObject { ["path"] = turn.Path ?? "src/app.ts" } }
                }
            }
        };
        await WriteAsync(output, request.ToJsonString(), cancellationToken);
        if (duplicate)
            await WriteAsync(output, request.ToJsonString(), cancellationToken);
        if (promptId is not null)
            await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn", ["completed"] = true }).ToJsonString(), cancellationToken);
    }

    private static Task NotifyToolAsync(TextWriter output, string name, CancellationToken cancellationToken, string? error = null, string? arguments = null)
    {
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "session/update",
            ["params"] = new JsonObject
            {
                ["sessionUpdate"] = "tool_call",
                ["title"] = name,
                ["kind"] = "tool",
                ["status"] = error is null ? "completed" : "failed",
                ["content"] = error ?? arguments ?? ""
            }
        };
        return WriteAsync(output, payload.ToJsonString(), cancellationToken);
    }

    private static JsonObject Initialize(bool httpMcp) => new()
    {
        ["protocolVersion"] = 1,
        ["agentCapabilities"] = new JsonObject
        {
            ["loadSession"] = true,
            ["mcpCapabilities"] = new JsonObject { ["http"] = httpMcp }
        },
        ["authMethods"] = new JsonArray()
    };

    /// <summary>
    /// Makes each directed call against the server it names, as a real agent would: a pending
    /// tool call with its input, then a completed or failed update with what the tool returned.
    /// A server the agent was not offered fails that call rather than the turn.
    /// </summary>
    private async Task CallToolsAsync(
        IReadOnlyList<ScriptedMcpCall> calls,
        IReadOnlyList<ScriptedMcpServer> servers,
        string sessionId,
        JsonNode? promptId,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var failed = 0;
        foreach (var (call, index) in calls.Select((call, index) => (call, index)))
        {
            var toolCallId = $"mcp-{index + 1}";
            var title = $"{call.Server}_{call.Tool}";
            await UpdateAsync(output, sessionId, new JsonObject
            {
                ["sessionUpdate"] = "tool_call",
                ["toolCallId"] = toolCallId,
                ["title"] = title,
                ["kind"] = "fetch",
                ["status"] = "pending",
                ["rawInput"] = call.Arguments.DeepClone()
            }, cancellationToken);
            var server = servers.FirstOrDefault(offered => offered.Name == call.Server);
            var result = server is null
                ? new ScriptedMcpResult(true, $"{call.Server} was not offered to this session.")
                : await mcp!.CallAsync(server, call.Tool, call.Arguments, cancellationToken);
            failed += result.IsError ? 1 : 0;
            await UpdateAsync(output, sessionId, new JsonObject
            {
                ["sessionUpdate"] = "tool_call_update",
                ["toolCallId"] = toolCallId,
                ["title"] = title,
                ["status"] = result.IsError ? "failed" : "completed",
                ["rawOutput"] = new JsonObject { ["output"] = result.Text },
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "content",
                        ["content"] = new JsonObject { ["type"] = "text", ["text"] = result.Text }
                    }
                }
            }, cancellationToken);
        }
        await UpdateAsync(output, sessionId, new JsonObject
        {
            ["sessionUpdate"] = "agent_message_chunk",
            ["content"] = new JsonObject
            {
                ["type"] = "text",
                ["text"] = $"Called {calls.Count} tool(s); {failed} failed."
            }
        }, cancellationToken);
        await WriteAsync(output, Result(promptId, new JsonObject { ["stopReason"] = "end_turn" }).ToJsonString(), cancellationToken);
    }

    private static Task UpdateAsync(TextWriter output, string sessionId, JsonObject update, CancellationToken cancellationToken) =>
        WriteAsync(output, new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "session/update",
            ["params"] = new JsonObject { ["sessionId"] = sessionId, ["update"] = update }
        }.ToJsonString(), cancellationToken);

    private static JsonNode Result(JsonNode? id, JsonObject result) => new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result
    };

    private static async Task WriteAsync(TextWriter output, string line, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync(line);
        await output.FlushAsync(cancellationToken);
    }
}
