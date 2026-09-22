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
    private readonly IAgentSubscriptionLoginProvider login;
    private readonly IAgentProcessEnvironmentProvider environment;
    private readonly IAgentClaudeCredentialStore credentials;
    private readonly AgentSubscriptionAuth auth;
    private readonly AgentEventFrameProvider payloads;
    private readonly AgentEventService events;
    private readonly ILogger<AgentSchedulingService> logger;
    private readonly AgentSessionRepository? sessionRepository;
    private readonly ConcurrentDictionary<string, AgentSessionState> _sessions = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _lifecycleGates = new();
    private IReadOnlyList<AgentDescriptor> _descriptors = Enum.GetValues<AgentKind>()
        .Select(kind => new AgentDescriptor(kind, false, DisplayName(kind)))
        .ToArray();

    public AgentSchedulingService(
        WorkspaceQueryController workspaces,
        IAgentProcessFactory processes,
        AgentCommandProvider commands,
        IAgentSubscriptionLoginProvider login,
        IAgentProcessEnvironmentProvider environment,
        IAgentClaudeCredentialStore credentials,
        AgentSubscriptionAuth auth,
        AgentEventFrameProvider payloads,
        AgentEventService events,
        ILogger<AgentSchedulingService> logger,
        AgentSessionRepository? sessionRepository = null)
    {
        this.workspaces = workspaces;
        this.processes = processes;
        this.commands = commands;
        this.login = login;
        this.environment = environment;
        this.credentials = credentials;
        this.auth = auth;
        this.payloads = payloads;
        this.events = events;
        this.logger = logger;
        this.sessionRepository = sessionRepository;
        workspaces.WorkspaceRemoved += HandleWorkspaceRemoved;
    }

    public AgentSessionDto? Get(string workspaceId) => SessionDto(workspaceId);

    public async Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        _descriptors = await DescriptorsAsync(cancellationToken);
        return SessionDto(workspaceId);
    }

    private AgentSessionDto? SessionDto(string workspaceId)
    {
        if (workspaces.GetById(workspaceId) is null) return null;
        var session = _sessions.GetValueOrDefault(workspaceId);
        var saved = sessionRepository?.List(workspaceId)
            .Select(item => new AgentSessionSummaryDto(item.SessionId, item.Agent, item.Description, item.Branch, item.LastUsedAt)).ToArray()
            ?? [];
        return new AgentSessionDto(workspaceId, session?.Kind, session?.State ?? "idle",
            session?.AcpSessionId, session?.Error, _descriptors, session?.AuthMethods ?? [], session?.LoginChallenge, saved);
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
        var gate = LifecycleGate(workspaceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var workspace = workspaces.GetById(workspaceId);
            if (workspace is null) return new AgentScheduleResult(null, false, null);
            if (!await commands.IsAvailableAsync(kind, cancellationToken)) throw new InvalidOperationException($"{kind} ACP executable is not installed or is not on PATH.");
            if (_sessions.ContainsKey(workspaceId))
                await StopCoreAsync(workspaceId, cancellationToken);

            var state = new AgentSessionState(kind, workspace.WorktreePath, processes.Create());
            if (!_sessions.TryAdd(workspaceId, state)) throw new InvalidOperationException("This workspace already has an agent.");
            BindProcess(workspaceId, state, state.Process);
            try
            {
                await state.Process.StartAsync(kind, workspace.WorktreePath, environment.EnvironmentFor(kind), cancellationToken);
                await InitializeAsync(workspaceId, state, cancellationToken);
                await CreateSessionAsync(state, cancellationToken);
                SaveSession(workspaceId, state, workspace.Branch, $"New {DisplayName(kind)} session");
                var scheduled = await GetAsync(workspaceId, cancellationToken);
                events.Publish(workspaceId, "state", scheduled!);
                return new AgentScheduleResult(scheduled, true, null);
            }
            catch (InvalidOperationException exception) when (
                state.AcpSessionId is null &&
                state.State != "stopped" &&
                exception.Message != MissingSessionId &&
                ShouldOfferLogin(state, exception))
            {
                if (state.AuthMethods.Count == 0)
                    state.AuthMethods = auth.Defaults(kind);
                state.State = "authentication_required";
                state.Error = exception.Message;
                var pendingAuth = await GetAsync(workspaceId, cancellationToken);
                events.Publish(workspaceId, "state", pendingAuth!);
                return new AgentScheduleResult(pendingAuth, true, null);
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
        finally { gate.Release(); }
    }

    public async Task<AgentScheduleResult> ResumeAsync(string workspaceId, string sessionId, CancellationToken cancellationToken)
    {
        var gate = LifecycleGate(workspaceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var workspace = workspaces.GetById(workspaceId);
            if (workspace is null) return new AgentScheduleResult(null, false, null);
            var saved = sessionRepository?.Find(workspaceId, sessionId);
            if (saved is null) return new AgentScheduleResult(null, false, null);
            if (!await commands.IsAvailableAsync(saved.Agent, cancellationToken))
                return new AgentScheduleResult(null, true, $"{saved.Agent} ACP executable is not installed or is not on PATH.");
            if (_sessions.TryGetValue(workspaceId, out var current) && current.AcpSessionId == sessionId && current.State != "stopped")
                return new AgentScheduleResult(await GetAsync(workspaceId, cancellationToken), true, null);
            if (_sessions.ContainsKey(workspaceId)) await StopCoreAsync(workspaceId, cancellationToken);

            var state = new AgentSessionState(saved.Agent, workspace.WorktreePath, processes.Create());
            _sessions[workspaceId] = state;
            BindProcess(workspaceId, state, state.Process);
            try
            {
                await state.Process.StartAsync(saved.Agent, workspace.WorktreePath, environment.EnvironmentFor(saved.Agent), cancellationToken);
                await InitializeAsync(workspaceId, state, cancellationToken);
                var loaded = await state.Process.CallAsync("session/load", new { sessionId, cwd = workspace.WorktreePath, mcpServers = Array.Empty<object>() }, cancellationToken);
                var loadedSessionId = ReadOptionalSessionId(loaded);
                state.AcpSessionId = string.IsNullOrWhiteSpace(loadedSessionId) ? sessionId : loadedSessionId;
                state.State = "ready";
                var persisted = SavedSession(workspaceId, state, workspace.Branch, saved.Description);
                if (persisted is not null) sessionRepository?.Rekey(persisted, sessionId);
                var result = await GetAsync(workspaceId, cancellationToken);
                events.Publish(workspaceId, "state", result!);
                return new AgentScheduleResult(result, true, null);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
            {
                _sessions.TryRemove(workspaceId, out _);
                await state.Process.DisposeAsync();
                state.Lifetime.Dispose(); state.PromptGate.Dispose();
                return new AgentScheduleResult(null, true, exception.Message);
            }
        }
        finally { gate.Release(); }
    }

    public AgentActionResult Authenticate(string workspaceId, string methodId)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return AgentActionResult.NotFound();
        if (state.State != "authentication_required") return AgentActionResult.Failed("The workspace agent is not waiting for authentication.");
        if (!state.AuthMethods.Any(method => string.Equals(method.Id, methodId, StringComparison.Ordinal)))
            return AgentActionResult.Failed("The requested authentication method was not offered by the agent.");
        var offered = state.AuthMethods.First(method => string.Equals(method.Id, methodId, StringComparison.Ordinal));
        if (auth.IsApiKeyMethod(offered.Id, offered.Name))
            return AgentActionResult.Failed("Agent-Up signs agents in with a ChatGPT, Cursor, or Claude subscription, not an API key.");
        state.State = "authenticating";
        state.Error = null;
        state.LoginInbox = new AgentLoginInbox();
        events.Publish(workspaceId, "state", Get(workspaceId)!);
        _ = RunAuthenticationAsync(workspaceId, state, methodId);
        return AgentActionResult.Success();
    }

    /// <summary>
    /// Hands a code the user copied out of the provider page to the CLI that is waiting on it.
    /// </summary>
    public AgentActionResult SubmitLoginCode(string workspaceId, string code) =>
        Submit(workspaceId, new AgentLoginSubmission(AgentLoginSubmissionKind.Code, code));

    /// <summary>
    /// Hands back a redirect the client intercepted, so it can be replayed against the loopback
    /// address the agent CLI is listening on. This is what lets a phone finish a sign-in whose
    /// callback would otherwise land on the Server host and be lost.
    /// </summary>
    public AgentActionResult SubmitLoginCallback(string workspaceId, string url) =>
        Submit(workspaceId, new AgentLoginSubmission(AgentLoginSubmissionKind.Callback, url));

    private AgentActionResult Submit(string workspaceId, AgentLoginSubmission submission)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return AgentActionResult.NotFound();
        if (state.LoginInbox is not { } inbox || state.State != "authenticating")
            return AgentActionResult.Failed("The workspace agent is not signing in.");
        return inbox.TrySubmit(submission)
            ? AgentActionResult.Success()
            : AgentActionResult.Failed("The sign-in is no longer accepting input.");
    }

    private async Task RunAuthenticationAsync(string workspaceId, AgentSessionState state, string methodId)
    {
        try
        {
            var acpCommand = await commands.ResolveAsync(state.Kind, state.Lifetime.Token)
                ?? throw new InvalidOperationException($"{state.Kind} ACP executable is not installed or is not on PATH.");
            var inbox = state.LoginInbox ??= new AgentLoginInbox();
            var result = await login.LoginAsync(
                state.Kind,
                acpCommand,
                methodId,
                challenge =>
                {
                    state.LoginChallenge = challenge;
                    var pending = Get(workspaceId);
                    if (pending is not null) events.Publish(workspaceId, "state", pending);
                },
                inbox,
                state.Lifetime.Token);
            if (!result.Succeeded)
            {
                ReturnToAuthentication(state, result.Error, result.Challenge);
                return;
            }
            if (!string.IsNullOrWhiteSpace(result.ClaudeOAuthToken))
                credentials.Write(result.ClaudeOAuthToken);
            await RestartProcessAsync(workspaceId, state);
            await InitializeAsync(workspaceId, state, state.Lifetime.Token);
            await CreateSessionAsync(state, state.Lifetime.Token);
            state.LoginChallenge = null;
        }
        catch (OperationCanceledException) when (state.Lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            if (!state.Lifetime.IsCancellationRequested)
                ReturnToAuthentication(state, exception.Message, state.LoginChallenge);
        }
        finally
        {
            state.LoginInbox?.Complete();
            state.LoginInbox = null;
            var snapshot = Get(workspaceId);
            if (snapshot is not null) events.Publish(workspaceId, "state", snapshot);
        }
    }

    private static void ReturnToAuthentication(AgentSessionState state, string? error, AgentLoginChallengeDto? challenge)
    {
        state.State = "authentication_required";
        state.Error = error;
        if (challenge is not null && (challenge.Url is not null || challenge.Code is not null))
            state.LoginChallenge = challenge;
    }

    private async Task InitializeAsync(string workspaceId, AgentSessionState state, CancellationToken cancellationToken)
    {
        var initialized = await state.Process.CallAsync("initialize", new
        {
            protocolVersion = 1,
            clientCapabilities = new { fs = new { readTextFile = false, writeTextFile = false }, terminal = false, auth = new { terminal = false } },
            clientInfo = new { name = "Agent-Up", title = "Agent-Up", version = "1.0" }
        }, cancellationToken);
        if (!initialized.TryGetProperty("protocolVersion", out var version) || version.GetInt32() != 1)
            throw new InvalidOperationException("The configured agent does not support ACP protocol version 1.");
        var advertised = payloads.AuthMethods(initialized);
        var subscription = auth.KeepSubscription(advertised);
        state.AuthMethods = subscription.Count > 0 ? subscription : advertised.Count > 0 ? auth.Defaults(state.Kind) : state.AuthMethods;
        events.Publish(workspaceId, "initialized", initialized);
    }

    private async Task RestartProcessAsync(string workspaceId, AgentSessionState state)
    {
        var previous = state.Process;
        try { await previous.StopAsync(state.Lifetime.Token); }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        { logger.LogInformation(exception, "Could not stop the previous ACP process before subscription login restart."); }
        await previous.DisposeAsync();
        var next = processes.Create();
        state.Process = next;
        BindProcess(workspaceId, state, next);
        await next.StartAsync(state.Kind, state.WorkingDirectory, environment.EnvironmentFor(state.Kind), state.Lifetime.Token);
    }

    private void BindProcess(string workspaceId, AgentSessionState state, IAgentProcessProvider process)
    {
        process.Notification += (method, payload) => HandleNotificationAsync(workspaceId, method, payload);
        process.Request += (method, payload) => HandleRequestAsync(workspaceId, state, method, payload, state.Lifetime.Token);
        process.Exited += error => HandleExit(workspaceId, state, process, error);
    }

    private bool ShouldOfferLogin(AgentSessionState state, InvalidOperationException exception) =>
        state.AuthMethods.Count > 0 || auth.LooksLikeAuthenticationFailure(exception.Message);

    private async Task CreateSessionAsync(AgentSessionState state, CancellationToken cancellationToken)
    {
        var session = await state.Process.CallAsync("session/new", new { cwd = state.WorkingDirectory, mcpServers = Array.Empty<object>() }, cancellationToken);
        state.AcpSessionId = ReadSessionId(session);
        state.State = "ready";
        state.Error = null;
    }

    private static string ReadSessionId(JsonElement session)
    {
        if (!session.TryGetProperty("sessionId", out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException(MissingSessionId);
        var sessionId = value.GetString();
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new InvalidOperationException(MissingSessionId);
        return sessionId;
    }

    private static string? ReadOptionalSessionId(JsonElement session) =>
        session.ValueKind == JsonValueKind.Object && session.TryGetProperty("sessionId", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private void SaveSession(string workspaceId, AgentSessionState state, string branch, string description)
    {
        var session = SavedSession(workspaceId, state, branch, description);
        if (session is not null) sessionRepository?.Upsert(session);
    }

    private static PersistedAgentSession? SavedSession(string workspaceId, AgentSessionState state, string branch, string description) =>
        state.AcpSessionId is null ? null : new(workspaceId, state.AcpSessionId, state.Kind, description, branch, DateTimeOffset.UtcNow);

    public async Task<AgentActionResult> PromptAsync(string workspaceId, string message, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return AgentActionResult.NotFound();
        if (string.IsNullOrWhiteSpace(state.AcpSessionId) || state.State is not "ready" and not "running")
            return AgentActionResult.Failed("The workspace agent is not ready for a prompt.");
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
            var result = await state.Process.CallAsync("session/prompt", new
            {
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
        if (string.IsNullOrWhiteSpace(state.AcpSessionId))
            return AgentActionResult.Failed("The workspace agent has no ACP session.");
        if (state.State is not "running")
            return AgentActionResult.Success();
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
        var gate = LifecycleGate(workspaceId);
        await gate.WaitAsync(cancellationToken);
        try { return await StopCoreAsync(workspaceId, cancellationToken); }
        finally { gate.Release(); }
    }

    private async Task<AgentActionResult> StopCoreAsync(string workspaceId, CancellationToken cancellationToken)
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
        events.Remove(workspaceId);
        return error is null ? AgentActionResult.Success() : AgentActionResult.Failed(error);
    }

    private SemaphoreSlim LifecycleGate(string workspaceId) => _lifecycleGates.GetOrAdd(workspaceId, _ => new SemaphoreSlim(1, 1));

    private Task HandleNotificationAsync(string workspaceId, string method, JsonElement payload)
    {
        var update = payload.TryGetProperty("update", out var nested) ? nested : payload;
        if (method == "session/update" && _sessions.TryGetValue(workspaceId, out var state)
            && update.TryGetProperty("sessionUpdate", out var kind)
            && (kind.GetString()?.Contains("session_info", StringComparison.OrdinalIgnoreCase) == true
                || string.Equals(kind.GetString(), "title", StringComparison.OrdinalIgnoreCase))
            && update.TryGetProperty("title", out var title) && !string.IsNullOrWhiteSpace(title.GetString())
            && workspaces.GetById(workspaceId) is { } workspace)
            SaveSession(workspaceId, state, workspace.Branch, title.GetString()!);
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

    private void HandleExit(string workspaceId, AgentSessionState state, IAgentProcessProvider process, string? error)
    {
        if (!_sessions.TryGetValue(workspaceId, out var current) || !ReferenceEquals(current, state)) return;
        if (!ReferenceEquals(current.Process, process)) return;
        state.State = "stopped"; state.Error = error;
        if (error is null)
            logger.LogInformation("Agent for workspace {WorkspaceId} exited.", workspaceId);
        else
            logger.LogInformation("Agent for workspace {WorkspaceId} exited with an error.", workspaceId);
        var snapshot = Get(workspaceId);
        if (snapshot is not null) events.Publish(workspaceId, "state", snapshot);
    }

    private void HandleWorkspaceRemoved(string workspaceId) => _ = StopRemovedWorkspaceAsync(workspaceId);

    private async Task StopRemovedWorkspaceAsync(string workspaceId)
    {
        try { await StopAsync(workspaceId, CancellationToken.None); sessionRepository?.RemoveWorkspace(workspaceId); }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        { logger.LogWarning(exception, "Could not stop the agent for removed workspace {WorkspaceId}.", workspaceId); }
    }

    private async Task<IReadOnlyList<AgentDescriptor>> DescriptorsAsync(CancellationToken cancellationToken)
    {
        var descriptors = Enum.GetValues<AgentKind>().Select(async kind =>
            new AgentDescriptor(kind, await commands.IsAvailableAsync(kind, cancellationToken), DisplayName(kind)));
        return await Task.WhenAll(descriptors);
    }

    private const string MissingSessionId = "The ACP agent did not return a session ID.";

    private static string DisplayName(AgentKind kind) => kind switch
    {
        AgentKind.Codex => "Codex",
        AgentKind.Cursor => "Cursor",
        AgentKind.Claude => "Claude",
        _ => $"{kind}"
    };

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
        foreach (var gate in _lifecycleGates.Values) gate.Dispose();
        _lifecycleGates.Clear();
    }
}
