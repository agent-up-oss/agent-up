using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Tests.Fake;

/// <summary>
/// In-process scripted ACP agent that plays the same turn kinds as test-agent-scripted.
/// Server tests must not ProjectReference AgentUp.TestAgents.
/// </summary>
internal sealed class ScriptedAcpProcess : IAgentProcessProvider
{
    private readonly Queue<ScriptedTurnKind> _turns;
    private readonly string _sessionId = $"scripted-{Guid.NewGuid():N}";
    public event Func<string, JsonElement, Task>? Notification;
    public event Func<string, JsonElement, Task<JsonElement>>? Request;
    public event Action<string?>? Exited;
    public List<string> Prompts { get; } = [];
    public int RecoveryOffers { get; private set; }

    public ScriptedAcpProcess(params ScriptedTurnKind[] turns) =>
        _turns = new Queue<ScriptedTurnKind>(turns);

    public Task StartAsync(
        string agent,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        if (method == "session/new")
            return JsonSerializer.SerializeToElement(new { sessionId = _sessionId });
        if (method != "session/prompt")
            return JsonSerializer.SerializeToElement(new { protocolVersion = 1 });

        Prompts.Add(JsonSerializer.Serialize(parameters));
        var turn = _turns.Count == 0 ? ScriptedTurnKind.Complete : _turns.Dequeue();
        return await PlayAsync(turn, cancellationToken);
    }

    public Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<JsonElement> PlayAsync(ScriptedTurnKind turn, CancellationToken cancellationToken)
    {
        switch (turn)
        {
            case ScriptedTurnKind.Complete:
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
            case ScriptedTurnKind.EndTurn:
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
            case ScriptedTurnKind.RateLimited:
                return JsonSerializer.SerializeToElement(new { stopReason = "rate_limited" });
            case ScriptedTurnKind.Crash:
                throw new InvalidOperationException("scripted crash");
            case ScriptedTurnKind.Empty:
                return default;
            case ScriptedTurnKind.Whitespace:
                return JsonSerializer.SerializeToElement("   ");
            case ScriptedTurnKind.TruncatedJson:
                throw new JsonException("Truncated JSON mid-message.");
            case ScriptedTurnKind.HoldPipe:
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return default;
            case ScriptedTurnKind.Permission:
                await RequestPermissionAsync(duplicate: false);
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
            case ScriptedTurnKind.DuplicatePermission:
                await RequestPermissionAsync(duplicate: true);
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
            case ScriptedTurnKind.OutOfOrder:
                var result = JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
                _ = RequestPermissionAsync(duplicate: false);
                return result;
            case ScriptedTurnKind.ToolCall:
                await NotifyToolAsync("read_file");
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
            case ScriptedTurnKind.UnknownTool:
                await NotifyToolAsync("not_a_real_tool");
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
            case ScriptedTurnKind.ToolError:
                RecoveryOffers++;
                await NotifyToolAsync("read_file", failed: true);
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
            case ScriptedTurnKind.HugeArgs:
                await NotifyToolAsync("write_file", arguments: new string('x', 3_000_000));
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
            case ScriptedTurnKind.InvalidUtf8:
                await NotifyToolAsync("read_file", title: "\uD800");
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
            default:
                return JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
        }
    }

    private async Task RequestPermissionAsync(bool duplicate)
    {
        var handler = Request ?? throw new InvalidOperationException("Permission request handler was not registered.");
        var payload = JsonSerializer.SerializeToElement(new
        {
            options = new[]
            {
                new { optionId = "allow-once", kind = "allow_once" },
                new { optionId = "reject-once", kind = "reject_once" }
            }
        });
        await handler("session/request_permission", payload);
        if (duplicate)
            await handler("session/request_permission", payload);
    }

    private Task NotifyToolAsync(string name, bool failed = false, string? arguments = null, string? title = null)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            sessionUpdate = "tool_call",
            title = title ?? name,
            kind = "tool",
            status = failed ? "failed" : "completed",
            content = arguments ?? (failed ? "ENOENT" : "")
        });
        return Notification?.Invoke("session/update", payload) ?? Task.CompletedTask;
    }
}
