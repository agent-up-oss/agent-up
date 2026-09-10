using System.Collections.Concurrent;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Agents.Services;

public sealed class AgentSchedulingService(
    WorkspaceQueryController workspaces,
    AgentProcessFactory processes,
    AgentCommandProvider commands,
    AgentEventFrameProvider payloads,
    AgentEventService events,
    ILogger<AgentSchedulingService> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, AgentSessionState> _sessions = new();

    public AgentSessionDto Get(string workspaceId)
    {
        var session = _sessions.GetValueOrDefault(workspaceId);
        return new AgentSessionDto(workspaceId, session?.Kind, session?.State ?? "idle",
            session?.AcpSessionId, session?.Error, Descriptors());
    }

    public async Task<AgentScheduleResult> ScheduleAsync(string workspaceId, AgentKind kind, CancellationToken cancellationToken)
    {
        try { return await ScheduleCoreAsync(workspaceId, kind, cancellationToken); }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        { return new AgentScheduleResult(null, true, exception.Message); }
    }

    private async Task<AgentScheduleResult> ScheduleCoreAsync(string workspaceId, AgentKind kind, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null) return new AgentScheduleResult(null, false, null);
        if (!commands.IsAvailable(kind)) throw new InvalidOperationException($"{kind} ACP executable is not installed or is not on PATH.");
        if (_sessions.ContainsKey(workspaceId)) throw new InvalidOperationException("This workspace already has an agent. Stop it before selecting another agent.");

        var state = new AgentSessionState(kind, processes.Create());
        if (!_sessions.TryAdd(workspaceId, state)) throw new InvalidOperationException("This workspace already has an agent.");
        state.Process.Notification += (method, payload) => HandleNotificationAsync(workspaceId, method, payload);
        state.Process.Request += (method, payload) => HandleRequestAsync(workspaceId, state, method, payload, cancellationToken);
        state.Process.Exited += error => HandleExit(workspaceId, state, error);
        try
        {
            await state.Process.StartAsync(kind, workspace.WorktreePath, cancellationToken);
            var initialized = await state.Process.CallAsync("initialize", new {
                protocolVersion = 1,
                clientCapabilities = new { fs = new { readTextFile = false, writeTextFile = false }, terminal = false },
                clientInfo = new { name = "Agent-Up", title = "Agent-Up", version = "1.0" }
            }, cancellationToken);
            events.Publish(workspaceId, "initialized", initialized);
            var session = await state.Process.CallAsync("session/new", new { cwd = workspace.WorktreePath, mcpServers = Array.Empty<object>() }, cancellationToken);
            state.AcpSessionId = session.GetProperty("sessionId").GetString()
                ?? throw new InvalidOperationException("The ACP agent did not return a session ID.");
            state.State = "ready";
            events.Publish(workspaceId, "state", Get(workspaceId));
            return new AgentScheduleResult(Get(workspaceId), true, null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _sessions.TryRemove(workspaceId, out _);
            await state.Process.DisposeAsync();
            throw;
        }
    }

    public async Task<bool> PromptAsync(string workspaceId, string message, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return false;
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is required.", nameof(message));
        await state.PromptGate.WaitAsync(cancellationToken);
        try
        {
            state.State = "running";
            events.Publish(workspaceId, "user_message", new { text = message });
            events.Publish(workspaceId, "state", Get(workspaceId));
            var result = await state.Process.CallAsync("session/prompt", new {
                sessionId = state.AcpSessionId,
                prompt = new[] { new { type = "text", text = message } }
            }, cancellationToken);
            events.Publish(workspaceId, "prompt_result", result);
            state.State = "ready";
            events.Publish(workspaceId, "state", Get(workspaceId));
            return true;
        }
        finally { state.PromptGate.Release(); }
    }

    public async Task<bool> CancelAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return false;
        await state.Process.NotifyAsync("session/cancel", new { sessionId = state.AcpSessionId }, cancellationToken);
        state.State = "ready";
        events.Publish(workspaceId, "state", Get(workspaceId));
        return true;
    }

    public bool Decide(string workspaceId, AgentPermissionResponse response)
    {
        return _sessions.TryGetValue(workspaceId, out var state)
            && state.Permissions.TryRemove(response.RequestId, out var completion)
            && completion.TrySetResult(response.OptionId);
    }

    public async Task<bool> StopAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryRemove(workspaceId, out var state)) return false;
        foreach (var permission in state.Permissions.Values) permission.TrySetCanceled(cancellationToken);
        await state.Process.StopAsync(cancellationToken);
        await state.Process.DisposeAsync();
        events.Publish(workspaceId, "state", Get(workspaceId));
        return true;
    }

    private Task HandleNotificationAsync(string workspaceId, string method, JsonElement payload)
    {
        events.Publish(workspaceId, method == "session/update" ? "session_update" : method, payload);
        return Task.CompletedTask;
    }

    private async Task<JsonElement> HandleRequestAsync(
        string workspaceId, AgentSessionState state, string method, JsonElement payload, CancellationToken cancellationToken)
    {
        if (method != "session/request_permission") throw new InvalidOperationException($"ACP client method '{method}' is not supported.");
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!state.Permissions.TryAdd(requestId, completion)) throw new InvalidOperationException("Could not register permission request.");
        events.Publish(workspaceId, "permission_request", new { requestId, request = payload });
        try
        {
            var optionId = await completion.Task.WaitAsync(cancellationToken);
            return payloads.Payload(new { outcome = new { outcome = "selected", optionId } });
        }
        finally { state.Permissions.TryRemove(requestId, out _); }
    }

    private void HandleExit(string workspaceId, AgentSessionState state, string? error)
    {
        if (!_sessions.TryGetValue(workspaceId, out var current) || !ReferenceEquals(current, state)) return;
        state.State = "stopped"; state.Error = error;
        logger.LogInformation("Agent for workspace {WorkspaceId} exited: {Error}", workspaceId, error);
        events.Publish(workspaceId, "state", Get(workspaceId));
    }

    private IReadOnlyList<AgentDescriptor> Descriptors() => Enum.GetValues<AgentKind>()
        .Select(kind => new AgentDescriptor(kind, commands.IsAvailable(kind), kind switch {
            AgentKind.Codex => "Codex", AgentKind.Cursor => "Cursor", AgentKind.Claude => "Claude", _ => kind.ToString()
        })).ToArray();

    public async ValueTask DisposeAsync()
    {
        foreach (var state in _sessions.Values) await state.Process.DisposeAsync();
        _sessions.Clear();
    }
}
