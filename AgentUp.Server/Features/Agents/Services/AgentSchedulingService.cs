using System.Collections.Concurrent;
using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Agents.Services;

public sealed class AgentSchedulingService : IAsyncDisposable
{
    private readonly WorkspaceQueryController workspaces;
    private readonly IAgentProcessFactory processes;
    private readonly AgentCommandProvider commands;
    private readonly AgentEventFrameProvider payloads;
    private readonly AgentEventService events;
    private readonly ILogger<AgentSchedulingService> logger;
    private readonly ConcurrentDictionary<string, AgentSessionState> _sessions = new();

    public AgentSchedulingService(
        WorkspaceQueryController workspaces,
        IAgentProcessFactory processes,
        AgentCommandProvider commands,
        AgentEventFrameProvider payloads,
        AgentEventService events,
        ILogger<AgentSchedulingService> logger)
    {
        this.workspaces = workspaces;
        this.processes = processes;
        this.commands = commands;
        this.payloads = payloads;
        this.events = events;
        this.logger = logger;
        workspaces.WorkspaceRemoved += HandleWorkspaceRemoved;
    }

    public AgentSessionDto? Get(string workspaceId)
    {
        if (workspaces.GetById(workspaceId) is null) return null;
        var session = _sessions.GetValueOrDefault(workspaceId);
        return new AgentSessionDto(workspaceId, session?.Kind, session?.State ?? "idle",
            session?.AcpSessionId, session?.Error, Descriptors(), session?.AuthMethods ?? []);
    }

    public async Task<AgentScheduleResult> ScheduleAsync(string workspaceId, AgentKind kind, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind)) return new AgentScheduleResult(null, true, "The requested agent kind is not supported.");
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

        var state = new AgentSessionState(kind, workspace.WorktreePath, processes.Create());
        if (!_sessions.TryAdd(workspaceId, state)) throw new InvalidOperationException("This workspace already has an agent.");
        state.Process.Notification += (method, payload) => HandleNotificationAsync(workspaceId, method, payload);
        state.Process.Request += (method, payload) => HandleRequestAsync(workspaceId, state, method, payload, state.Lifetime.Token);
        state.Process.Exited += error => HandleExit(workspaceId, state, error);
        try
        {
            await state.Process.StartAsync(kind, workspace.WorktreePath, cancellationToken);
            var initialized = await state.Process.CallAsync("initialize", new {
                protocolVersion = 1,
                clientCapabilities = new { fs = new { readTextFile = false, writeTextFile = false }, terminal = false, auth = new { terminal = false } },
                clientInfo = new { name = "Agent-Up", title = "Agent-Up", version = "1.0" }
            }, cancellationToken);
            if (!initialized.TryGetProperty("protocolVersion", out var version) || version.GetInt32() != 1)
                throw new InvalidOperationException("The configured agent does not support ACP protocol version 1.");
            state.AuthMethods = payloads.AuthMethods(initialized);
            events.Publish(workspaceId, "initialized", initialized);
            await CreateSessionAsync(state, cancellationToken);
            events.Publish(workspaceId, "state", Get(workspaceId)!);
            return new AgentScheduleResult(Get(workspaceId), true, null);
        }
        catch (InvalidOperationException exception) when (state.AuthMethods.Count > 0 && state.AcpSessionId is null && state.State != "stopped")
        {
            state.State = "authentication_required";
            state.Error = exception.Message;
            events.Publish(workspaceId, "state", Get(workspaceId)!);
            return new AgentScheduleResult(Get(workspaceId), true, null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _sessions.TryRemove(workspaceId, out _);
            state.Lifetime.Cancel();
            await state.Process.DisposeAsync();
            state.Lifetime.Dispose();
            state.PromptGate.Dispose();
            throw;
        }
    }

    public AgentActionResult Authenticate(string workspaceId, string methodId)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return AgentActionResult.NotFound();
        if (state.State != "authentication_required") return AgentActionResult.Failed("The workspace agent is not waiting for authentication.");
        if (!state.AuthMethods.Any(method => string.Equals(method.Id, methodId, StringComparison.Ordinal)))
            return AgentActionResult.Failed("The requested authentication method was not offered by the agent.");
        state.State = "authenticating";
        state.Error = null;
        events.Publish(workspaceId, "state", Get(workspaceId)!);
        _ = RunAuthenticationAsync(workspaceId, state, methodId);
        return AgentActionResult.Success();
    }

    private async Task RunAuthenticationAsync(string workspaceId, AgentSessionState state, string methodId)
    {
        try
        {
            await state.Process.CallAsync("authenticate", new { methodId }, state.Lifetime.Token);
            await CreateSessionAsync(state, state.Lifetime.Token);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            if (!state.Lifetime.IsCancellationRequested) { state.State = "authentication_required"; state.Error = exception.Message; }
        }
        var snapshot = Get(workspaceId);
        if (snapshot is not null) events.Publish(workspaceId, "state", snapshot);
    }

    private async Task CreateSessionAsync(AgentSessionState state, CancellationToken cancellationToken)
    {
        var session = await state.Process.CallAsync("session/new", new { cwd = state.WorkingDirectory, mcpServers = Array.Empty<object>() }, cancellationToken);
        state.AcpSessionId = session.GetProperty("sessionId").GetString()
            ?? throw new InvalidOperationException("The ACP agent did not return a session ID.");
        state.State = "ready";
        state.Error = null;
    }

    public async Task<AgentActionResult> PromptAsync(string workspaceId, string message, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return AgentActionResult.NotFound();
        if (!await state.PromptGate.WaitAsync(TimeSpan.Zero, cancellationToken))
            return AgentActionResult.Failed("The workspace agent is already processing a prompt.");
        state.State = "running";
        state.Error = null;
        events.Publish(workspaceId, "user_message", new { text = message });
        events.Publish(workspaceId, "state", Get(workspaceId)!);
        _ = RunPromptAsync(workspaceId, state, message);
        return AgentActionResult.Success();
    }

    private async Task RunPromptAsync(string workspaceId, AgentSessionState state, string message)
    {
        try
        {
            var result = await state.Process.CallAsync("session/prompt", new {
                sessionId = state.AcpSessionId,
                prompt = new[] { new { type = "text", text = message } }
            }, state.Lifetime.Token);
            events.Publish(workspaceId, "prompt_result", result);
            state.State = "ready";
            events.Publish(workspaceId, "state", Get(workspaceId)!);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            if (!state.Lifetime.IsCancellationRequested)
            {
                state.State = "ready";
                state.Error = exception.Message;
                events.Publish(workspaceId, "state", Get(workspaceId)!);
            }
        }
        finally { state.PromptGate.Release(); }
    }

    public async Task<AgentActionResult> CancelAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return AgentActionResult.NotFound();
        try
        {
            await state.Process.NotifyAsync("session/cancel", new { sessionId = state.AcpSessionId }, cancellationToken);
            state.State = "ready";
            events.Publish(workspaceId, "state", Get(workspaceId)!);
            return AgentActionResult.Success();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            return AgentActionResult.Failed(exception.Message);
        }
    }

    public bool Decide(string workspaceId, AgentPermissionResponse response)
    {
        return _sessions.TryGetValue(workspaceId, out var state)
            && state.Permissions.TryGetValue(response.RequestId, out var permission)
            && permission.OptionIds.Contains(response.OptionId)
            && state.Permissions.TryRemove(response.RequestId, out _)
            && permission.Completion.TrySetResult(response.OptionId);
    }

    public async Task<AgentActionResult> StopAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryRemove(workspaceId, out var state)) return AgentActionResult.NotFound();
        state.Lifetime.Cancel();
        foreach (var permission in state.Permissions.Values) permission.Completion.TrySetCanceled(cancellationToken);
        string? error = null;
        try { await state.Process.StopAsync(cancellationToken); }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        { error = exception.Message; }
        await state.Process.DisposeAsync();
        state.Lifetime.Dispose();
        state.PromptGate.Dispose();
        events.Publish(workspaceId, "state", Get(workspaceId)!);
        return error is null ? AgentActionResult.Success() : AgentActionResult.Failed(error);
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
        var optionIds = payloads.PermissionOptionIds(payload);
        if (optionIds.Count == 0) throw new InvalidOperationException("The ACP agent requested permission without valid options.");
        if (!state.Permissions.TryAdd(requestId, new PendingAgentPermission(optionIds, completion))) throw new InvalidOperationException("Could not register permission request.");
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
        var snapshot = Get(workspaceId);
        if (snapshot is not null) events.Publish(workspaceId, "state", snapshot);
    }

    private void HandleWorkspaceRemoved(string workspaceId) => _ = StopRemovedWorkspaceAsync(workspaceId);

    private async Task StopRemovedWorkspaceAsync(string workspaceId)
    {
        try { await StopAsync(workspaceId, CancellationToken.None); }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        { logger.LogWarning(exception, "Could not stop the agent for removed workspace {WorkspaceId}.", workspaceId); }
    }

    private IReadOnlyList<AgentDescriptor> Descriptors() => Enum.GetValues<AgentKind>()
        .Select(kind => new AgentDescriptor(kind, commands.IsAvailable(kind), kind switch {
            AgentKind.Codex => "Codex", AgentKind.Cursor => "Cursor", AgentKind.Claude => "Claude", _ => $"{kind}"
        })).ToArray();

    public async ValueTask DisposeAsync()
    {
        workspaces.WorkspaceRemoved -= HandleWorkspaceRemoved;
        foreach (var state in _sessions.Values)
        {
            state.Lifetime.Cancel();
            await state.Process.DisposeAsync();
            state.Lifetime.Dispose();
            state.PromptGate.Dispose();
        }
        _sessions.Clear();
    }
}
