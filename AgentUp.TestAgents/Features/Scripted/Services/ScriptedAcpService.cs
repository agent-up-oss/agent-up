using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Scripted.Interfaces;
using AgentUp.TestAgents.Features.Scripted.Models;

namespace AgentUp.TestAgents.Features.Scripted.Services;

/// <summary>
/// A deterministic ACP v1 agent that emits a declared sequence of protocol turns.
/// session/new is immediate; there is no sign-in.
/// </summary>
public sealed class ScriptedAcpService(IScriptedTurnScript scripts)
{
    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        var script = scripts.Load();
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
                "initialize" => Result(id, Initialize()),
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

    private static JsonObject Initialize() => new()
    {
        ["protocolVersion"] = 1,
        ["agentCapabilities"] = new JsonObject { ["loadSession"] = true },
        ["authMethods"] = new JsonArray()
    };

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
