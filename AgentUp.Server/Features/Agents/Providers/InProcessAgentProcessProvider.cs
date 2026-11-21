using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// An agent that runs inside the Server. It answers the same ACP v1 calls an agent CLI answers
/// (<c>initialize</c>, <c>session/new</c>, <c>session/prompt</c>, <c>session/cancel</c>) so the
/// scheduling service, permission policy, autonomous loop, transcript, and clients treat it like
/// any other agent. It has no process, no filesystem, and no shell: each prompt is a bounded
/// conversation with an Ollama-compatible chat model whose only tools come from
/// <see cref="IInProcessToolHost"/>, pinned to this one workspace. Every host tool call is put to
/// the workspace permission path first as an ACP <c>fetch</c> request.
/// </summary>
public sealed class InProcessAgentProcessProvider(
    string workspaceId,
    IInProcessChatModel model,
    IInProcessToolHost tools,
    ILogger<InProcessAgentProcessProvider> logger,
    int maxToolRounds = 24) : IAgentProcessProvider
{
    private const string SystemPrompt =
        "You are a product reviewer on an Agent-Up team. You cannot read or change source code. " +
        "You see the product only through the browser and application tools you are given, which " +
        "already target your own workspace. Exercise the running application the way a user would, " +
        "take screenshots to see what is on screen, and judge it against the task. When the task asks " +
        "for a review, submit it with submit_review. Call complete_task when you are done.";

    private readonly List<InProcessChatMessage> _history = [];
    private readonly SemaphoreSlim _prompts = new(1, 1);
    private CancellationTokenSource? _turn;
    private string? _sessionId;
    private bool _started;
    private bool _stopped;

    public event Func<string, JsonElement, Task>? Notification;
    public event Func<string, JsonElement, Task<JsonElement>>? Request;
    public event Action<string?>? Exited;

    public Task StartAsync(
        string agent,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        if (_started)
            throw new InvalidOperationException("The in-process agent has already started.");
        if (!model.Configured)
            throw new InvalidOperationException($"{agent} needs {InProcessAgents.OllamaModelSetting} on the Server.");
        _started = true;
        return Task.CompletedTask;
    }

    public async Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        EnsureRunning();
        return method switch
        {
            "initialize" => Json(new
            {
                protocolVersion = 1,
                agentCapabilities = new { loadSession = false },
                authMethods = Array.Empty<object>()
            }),
            "session/new" => NewSession(),
            "session/prompt" => await PromptAsync(parameters, cancellationToken),
            "session/load" => throw new InvalidOperationException("The in-process agent keeps no session history to load."),
            _ => throw new InvalidOperationException($"The in-process agent does not implement '{method}'.")
        };
    }

    public Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        if (method == "session/cancel")
            _turn?.Cancel();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopped)
            return Task.CompletedTask;
        _stopped = true;
        _turn?.Cancel();
        Exited?.Invoke(null);
        return Task.CompletedTask;
    }

    private JsonElement NewSession()
    {
        _sessionId = $"in-process-{Guid.NewGuid():N}";
        _history.Clear();
        _history.Add(new InProcessChatMessage("system", SystemPrompt));
        return Json(new { sessionId = _sessionId });
    }

    private async Task<JsonElement> PromptAsync(object? parameters, CancellationToken cancellationToken)
    {
        await _prompts.WaitAsync(cancellationToken);
        using var turn = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _turn = turn;
        try
        {
            _history.Add(new InProcessChatMessage("user", PromptText(parameters)));
            var offered = await tools.ListAsync(workspaceId, turn.Token);
            var completed = false;
            for (var round = 0; round < maxToolRounds; round++)
            {
                var reply = await model.ChatAsync(_history, offered, turn.Token);
                _history.Add(reply);
                if (!string.IsNullOrWhiteSpace(reply.Content))
                    await UpdateAsync(new { sessionUpdate = "agent_message_chunk", content = new { type = "text", text = reply.Content } });
                if (reply.ToolCalls is not { Count: > 0 } calls)
                    return Json(new { stopReason = "end_turn", completed });

                foreach (var call in calls)
                {
                    var result = await RunToolAsync(call, offered, turn.Token);
                    completed |= result.CompletesTask && !result.IsError;
                    _history.Add(new InProcessChatMessage("tool", result.Text, ToolName: call.Name,
                        Images: result.Images.Count == 0 ? null : result.Images));
                }

                if (completed)
                    return Json(new { stopReason = "end_turn", completed = true });
            }

            return Json(new { stopReason = "max_turn_requests", completed });
        }
        catch (OperationCanceledException) when (turn.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Json(new { stopReason = "cancelled", completed = false });
        }
        finally
        {
            _turn = null;
            _prompts.Release();
        }
    }

    private async Task<InProcessToolResult> RunToolAsync(
        InProcessToolCall call,
        IReadOnlyList<InProcessTool> offered,
        CancellationToken cancellationToken)
    {
        var toolCallId = $"call-{Guid.NewGuid():N}";
        await UpdateAsync(new { sessionUpdate = "tool_call", toolCallId, title = call.Name, kind = "fetch", status = "pending", rawInput = call.Arguments });
        if (offered.All(tool => tool.Name != call.Name))
            return await FinishAsync(toolCallId, new InProcessToolResult($"Tool '{call.Name}' is not available to you.", [], true));
        if (call.Name != "complete_task" && !await PermittedAsync(toolCallId, call, cancellationToken))
            return await FinishAsync(toolCallId, new InProcessToolResult($"Permission to run '{call.Name}' was refused.", [], true));

        InProcessToolResult result;
        try
        {
            result = await tools.CallAsync(workspaceId, call.Name, call.Arguments, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogInformation(exception, "In-process agent tool {Tool} failed.", call.Name);
            result = new InProcessToolResult(exception.Message, [], true);
        }

        return await FinishAsync(toolCallId, result);
    }

    private async Task<bool> PermittedAsync(string toolCallId, InProcessToolCall call, CancellationToken cancellationToken)
    {
        if (Request is null)
            return false;
        var answer = await Request("session/request_permission", Json(new
        {
            sessionId = _sessionId,
            toolCall = new { toolCallId, title = $"fetch {call.Name}", kind = "fetch", rawInput = call.Arguments },
            options = new[]
            {
                new { optionId = "allow_once", name = "Allow", kind = "allow_once" },
                new { optionId = "reject_once", name = "Reject", kind = "reject_once" }
            }
        })).WaitAsync(cancellationToken);
        return answer.TryGetProperty("outcome", out var outcome)
               && outcome.TryGetProperty("optionId", out var option)
               && option.GetString()?.StartsWith("allow", StringComparison.Ordinal) == true;
    }

    private async Task<InProcessToolResult> FinishAsync(string toolCallId, InProcessToolResult result)
    {
        await UpdateAsync(new
        {
            sessionUpdate = "tool_call_update",
            toolCallId,
            status = result.IsError ? "failed" : "completed",
            content = new[] { new { type = "content", content = new { type = "text", text = result.Text } } }
        });
        return result;
    }

    private Task UpdateAsync(object update) =>
        Notification?.Invoke("session/update", Json(new { sessionId = _sessionId, update })) ?? Task.CompletedTask;

    private void EnsureRunning()
    {
        if (!_started || _stopped)
            throw new InvalidOperationException("The in-process agent is not running.");
    }

    private static string PromptText(object? parameters)
    {
        var element = parameters is JsonElement json ? json : JsonSerializer.SerializeToElement(parameters);
        if (!element.TryGetProperty("prompt", out var prompt) || prompt.ValueKind != JsonValueKind.Array)
            return "";
        return string.Join("\n", prompt.EnumerateArray()
            .Where(block => block.TryGetProperty("type", out var type) && type.GetString() == "text")
            .Select(block => block.TryGetProperty("text", out var text) ? text.GetString() : null)
            .Where(text => !string.IsNullOrEmpty(text)));
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    public ValueTask DisposeAsync()
    {
        _stopped = true;
        _turn?.Cancel();
        _prompts.Dispose();
        return ValueTask.CompletedTask;
    }
}
