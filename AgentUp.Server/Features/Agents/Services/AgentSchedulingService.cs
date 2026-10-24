using System.Collections.Concurrent;
using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Sdk.Agent;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Shared.Providers;
using Microsoft.Extensions.Hosting;

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
    private readonly IEnabledCapabilityPackages? packages;
    private readonly IAgentSessionRepository? sessionRepository;
    private readonly IAgentWorkspaceSettingsRepository? settings;
    private readonly IHostEnvironment? host;
    private readonly ConcurrentDictionary<string, AgentSessionState> _sessions = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _lifecycleGates = new();
    private IReadOnlyList<AgentDescriptor> _descriptors = FirstPartyAgents
        .Select(agent => new AgentDescriptor(agent, false, DisplayName(agent)))
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
        IEnabledCapabilityPackages? packages = null,
        IAgentSessionRepository? sessionRepository = null,
        IAgentWorkspaceSettingsRepository? settings = null,
        IHostEnvironment? host = null)
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
        this.packages = packages;
        this.sessionRepository = sessionRepository;
        this.settings = settings;
        this.host = host;
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
        var workspace = workspaces.GetById(workspaceId);
        var current = settings?.Get(workspaceId);
        return new AgentSessionDto(
            workspaceId,
            session?.Agent,
            session?.State ?? "idle",
            session?.AcpSessionId,
            session?.Error,
            _descriptors,
            session?.AuthMethods ?? [],
            session?.LoginChallenge,
            saved,
            workspace?.BoundAgentModuleId,
            LoopDto(current),
            RoleDto(current));
    }

    private static AgentLoopStatusDto? LoopDto(AgentWorkspaceSettings? current)
    {
        if (current?.Loop is null && current is not { AutonomyActive: true })
            return null;
        var loop = current.Loop;
        return new AgentLoopStatusDto(
            current.AutonomyActive,
            loop?.Phase ?? (current.AutonomyActive ? "idle" : "stopped"),
            loop?.CurrentTask?.Id,
            loop?.CurrentTask?.Number,
            loop?.CurrentTask?.Title,
            loop?.Terminal?.ToString(),
            loop?.StopReason,
            loop?.BlockReason,
            loop?.PermissionRuleIds ?? []);
    }

    private static AgentRoleRequest? RoleDto(AgentWorkspaceSettings? current) =>
        current?.Role is null
            ? null
            : new AgentRoleRequest
            {
                Prompt = current.Role.Prompt,
                Tools = current.Role.Tools,
                Trigger = current.Role.Trigger,
                Termination = current.Role.Termination,
                Review = current.Role.Review
            };

    public async Task<AgentScheduleResult> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agent)) return new AgentScheduleResult(null, true, "The requested agent is not supported.");
        try { return await ScheduleCoreAsync(workspaceId, agent, cancellationToken); }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        { return new AgentScheduleResult(null, true, exception.Message); }
    }

    public async Task<AgentBindingResult> BindAsync(string workspaceId, string agent, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null) return new AgentBindingResult(null, false, null);
        if (string.IsNullOrWhiteSpace(agent))
            return new AgentBindingResult(null, true, "The requested agent is not supported.");
        var agentId = NormalizeAgentId(agent);
        if (_sessions.TryGetValue(workspaceId, out var live)
            && !string.Equals(live.Agent, agentId, StringComparison.OrdinalIgnoreCase))
        {
            return new AgentBindingResult(
                await GetAsync(workspaceId, cancellationToken),
                true,
                $"This workspace already has a live {live.Agent} session '{live.AcpSessionId}'.",
                409);
        }

        await workspaces.UpdateBoundAgentAsync(workspaceId, agentId);
        return new AgentBindingResult(await GetAsync(workspaceId, cancellationToken), true, null, 200);
    }

    public async Task<AgentBindingResult> UnbindAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null) return new AgentBindingResult(null, false, null);
        if (_sessions.TryGetValue(workspaceId, out var live) && live.State is not "stopped" and not "idle")
        {
            return new AgentBindingResult(
                await GetAsync(workspaceId, cancellationToken),
                true,
                $"This workspace already has a live {live.Agent} session '{live.AcpSessionId}'.",
                409);
        }

        await workspaces.UpdateBoundAgentAsync(workspaceId, null);
        return new AgentBindingResult(await GetAsync(workspaceId, cancellationToken), true, null, 200);
    }

    private async Task<AgentScheduleResult> ScheduleCoreAsync(string workspaceId, string agent, CancellationToken cancellationToken)
    {
        var gate = LifecycleGate(workspaceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var workspace = workspaces.GetById(workspaceId);
            if (workspace is null) return new AgentScheduleResult(null, false, null);
            var agentId = NormalizeAgentId(agent);
            if (!await commands.IsAvailableAsync(agentId, cancellationToken)) throw new InvalidOperationException($"{agentId} ACP executable is not installed or is not on PATH.");
            if (_sessions.TryGetValue(workspaceId, out var live))
            {
                if (string.Equals(live.Agent, agentId, StringComparison.OrdinalIgnoreCase))
                    return new AgentScheduleResult(await GetAsync(workspaceId, cancellationToken), true, null);
                return new AgentScheduleResult(
                    null,
                    true,
                    $"This workspace already has a live {live.Agent} session '{live.AcpSessionId}'.");
            }
            if (!string.IsNullOrWhiteSpace(workspace.BoundAgentModuleId)
                && !string.Equals(workspace.BoundAgentModuleId, agentId, StringComparison.OrdinalIgnoreCase))
            {
                return new AgentScheduleResult(
                    null,
                    true,
                    $"This workspace is bound to '{workspace.BoundAgentModuleId}'. Unbind or change the bound agent before starting '{agentId}'.");
            }
            await workspaces.UpdateBoundAgentAsync(workspaceId, agentId);

            var state = new AgentSessionState(agentId, workspace.WorktreePath, processes.Create());
            if (!_sessions.TryAdd(workspaceId, state)) throw new InvalidOperationException("This workspace already has an agent.");
            BindProcess(workspaceId, state, state.Process);
            try
            {
                await state.Process.StartAsync(agentId, workspace.WorktreePath, environment.EnvironmentFor(agentId), cancellationToken);
                await InitializeAsync(workspaceId, state, cancellationToken);
                await CreateSessionAsync(state, cancellationToken);
                SaveSession(workspaceId, state, workspace.Branch, $"New {DisplayName(agentId)} session");
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
                    state.AuthMethods = auth.Defaults(agentId);
                state.State = "authentication_required";
                state.Error = auth.IsExpectedAuthenticationRequired(exception.Message)
                    ? null
                    : exception.Message;
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

    /// <summary>
    /// Restarts the workspace agent against a saved ACP session so the transcript the user
    /// left behind comes back instead of a blank one.
    /// </summary>
    public async Task<AgentScheduleResult> ResumeAsync(string workspaceId, string sessionId, CancellationToken cancellationToken)
    {
        var gate = LifecycleGate(workspaceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var workspace = workspaces.GetById(workspaceId);
            if (workspace is null) return new AgentScheduleResult(null, false, null);
            // No store means no history, so there is nothing for this workspace to resume.
            var repository = sessionRepository;
            if (repository is null) return new AgentScheduleResult(null, false, null);
            var saved = repository.Find(workspaceId, sessionId);
            if (saved is null) return new AgentScheduleResult(null, false, null);
            if (!string.IsNullOrWhiteSpace(workspace.BoundAgentModuleId)
                && !string.Equals(workspace.BoundAgentModuleId, saved.Agent, StringComparison.OrdinalIgnoreCase))
            {
                return new AgentScheduleResult(
                    null,
                    true,
                    $"This workspace is bound to '{workspace.BoundAgentModuleId}'. Session '{sessionId}' belongs to '{saved.Agent}'.");
            }
            if (string.IsNullOrWhiteSpace(workspace.BoundAgentModuleId))
                await workspaces.UpdateBoundAgentAsync(workspaceId, saved.Agent);
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
                // The agent may hand back a different id than the one we asked to load, so the
                // saved entry moves to that id rather than leaving a duplicate under the old one.
                state.AcpSessionId = string.IsNullOrWhiteSpace(loadedSessionId) ? sessionId : loadedSessionId;
                state.State = "ready";
                repository.Rekey(
                    new PersistedAgentSession(workspaceId, state.AcpSessionId, saved.Agent, saved.Description, workspace.Branch, DateTimeOffset.UtcNow),
                    sessionId);
                var result = await GetAsync(workspaceId, cancellationToken);
                events.Publish(workspaceId, "state", result!);
                return new AgentScheduleResult(result, true, null);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or System.ComponentModel.Win32Exception
                    or IOException
                    or UnauthorizedAccessException
                    or OperationCanceledException)
            {
                _sessions.TryRemove(workspaceId, out _);
                state.Lifetime.Cancel();
                await state.Process.DisposeAsync();
                state.Lifetime.Dispose();
                state.PromptGate.Dispose();
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
            var acpCommand = await commands.ResolveAsync(state.Agent, state.Lifetime.Token)
                ?? throw new InvalidOperationException($"{state.Agent} ACP executable is not installed or is not on PATH.");
            var inbox = state.LoginInbox ??= new AgentLoginInbox();
            var result = await login.LoginAsync(
                state.Agent,
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
            else if (auth.RequiresCapturedOauthToken(result.Challenge)
                     && string.IsNullOrWhiteSpace(credentials.Read()))
            {
                ReturnToAuthentication(
                    state,
                    "Sign-in finished without a subscription token. Paste the code from the page, then try again.",
                    result.Challenge);
                return;
            }
            await RestartProcessAsync(workspaceId, state);
            await InitializeAsync(workspaceId, state, state.Lifetime.Token);
            await CreateSessionAsync(state, state.Lifetime.Token);
            // Scheduling returned at authentication_required before it could save, so this is
            // the first point at which a session created behind sign-in exists to record.
            if (workspaces.GetById(workspaceId) is { } workspace)
                SaveSession(workspaceId, state, workspace.Branch, $"New {DisplayName(state.Agent)} session");
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
        var initialized = await state.Process.CallAsync("initialize", new {
            protocolVersion = 1,
            clientCapabilities = new { fs = new { readTextFile = false, writeTextFile = false }, terminal = false, auth = new { terminal = false } },
            clientInfo = new { name = "Agent-Up", title = "Agent-Up", version = "1.0" }
        }, cancellationToken);
        if (!initialized.TryGetProperty("protocolVersion", out var version) || version.GetInt32() != 1)
            throw new InvalidOperationException("The configured agent does not support ACP protocol version 1.");
        var advertised = payloads.AuthMethods(initialized);
        var subscription = auth.KeepSubscription(advertised);
        state.AuthMethods = subscription.Count > 0 ? subscription : advertised.Count > 0 ? auth.Defaults(state.Agent) : state.AuthMethods;
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
        await next.StartAsync(state.Agent, state.WorkingDirectory, environment.EnvironmentFor(state.Agent), state.Lifetime.Token);
    }

    private void BindProcess(string workspaceId, AgentSessionState state, IAgentProcessProvider process)
    {
        process.Notification += (method, payload) => HandleNotificationAsync(workspaceId, state, process, method, payload);
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
        if (state.AcpSessionId is null) return;
        sessionRepository?.Upsert(new PersistedAgentSession(workspaceId, state.AcpSessionId, state.Agent, description, branch, DateTimeOffset.UtcNow));
    }

    public async Task<AgentActionResult> PromptAsync(string workspaceId, string message, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return AgentActionResult.NotFound();
        if (string.IsNullOrWhiteSpace(state.AcpSessionId) || state.State is not "ready" and not "running")
            return AgentActionResult.Failed("The workspace agent is not ready for a prompt.");
        if (!await state.PromptGate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            if (state.PermissionPolicy is not null)
            {
                state.Steering.Enqueue(message);
                events.Publish(workspaceId, "user_message", new { text = message, steering = true });
                return AgentActionResult.Success();
            }
            return AgentActionResult.Failed("The workspace agent is already processing a prompt.");
        }
        state.State = "running";
        state.Error = null;
        events.Publish(workspaceId, "user_message", new { text = message });
        events.Publish(workspaceId, "state", Get(workspaceId)!);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        state.PromptCompletion = completion;
        state.LoopBlock = null;
        _ = RunPromptAsync(workspaceId, state, message, completion);
        return AgentActionResult.Success();
    }

    public async Task<AgentActionResult> PromptWaitAsync(string workspaceId, string message, CancellationToken cancellationToken)
    {
        var queued = await PromptAsync(workspaceId, message, cancellationToken);
        if (!queued.Found || queued.Error is not null) return queued;
        if (!_sessions.TryGetValue(workspaceId, out var state) || state.PromptCompletion is null)
            return queued;
        try
        {
            await state.PromptCompletion.Task.WaitAsync(cancellationToken);
            return AgentActionResult.Success();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            return AgentActionResult.Failed(exception.Message);
        }
    }

    public bool AttachPolicy(string workspaceId, IAgentPermissionPolicy? policy)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state)) return false;
        state.PermissionPolicy = policy;
        if (policy is null)
            state.LoopBlock = null;
        return true;
    }

    public void BeginLoopTask(string workspaceId)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state))
            return;
        state.TranscriptEvents = 0;
        state.PermissionRuleIds.Clear();
        state.LoopBlock = null;
        state.ConsecutiveNoProgress = 0;
        state.LastPromptResult = null;
        state.SawToolCall = false;
    }

    public LoopInspection InspectLoop(string workspaceId)
    {
        if (!_sessions.TryGetValue(workspaceId, out var state))
            return new LoopInspection([], true, true, 0, false, null, "not-found", false);
        var stop = ReadStopReason(state.LastPromptResult);
        var completed = ReadCompleted(state.LastPromptResult);
        return new LoopInspection(
            [.. state.PermissionRuleIds],
            state.Permissions.IsEmpty,
            state.PromptGate.CurrentCount == 1,
            state.TranscriptEvents,
            completed,
            stop,
            state.LoopBlock,
            state.SawToolCall);
    }

    private async Task RunPromptAsync(
        string workspaceId, AgentSessionState state, string message, TaskCompletionSource<JsonElement> completion)
    {
        Exception? failure = null;
        JsonElement result = default;
        var succeeded = false;
        try
        {
            state.SawToolCall = false;
            var delivered = DrainSteering(state, message);
            result = await state.Process.CallAsync("session/prompt", new {
                sessionId = state.AcpSessionId,
                prompt = new[] { new { type = "text", text = delivered } }
            }, state.Lifetime.Token);
            if (result.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                result = JsonSerializer.SerializeToElement(new { });
            state.LastPromptResult = result;
            events.Publish(workspaceId, "prompt_result", result);
            state.State = "ready";
            events.Publish(workspaceId, "state", Get(workspaceId)!);
            succeeded = true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            failure = exception;
            if (!state.Lifetime.IsCancellationRequested)
            {
                state.State = "ready";
                state.Error = exception.Message;
                events.Publish(workspaceId, "state", Get(workspaceId)!);
            }
        }
        finally { state.PromptGate.Release(); }

        if (succeeded)
            completion.TrySetResult(result);
        else if (failure is not null)
            completion.TrySetException(failure);
        else
            completion.TrySetCanceled();
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

    /// <summary>Stops the workspace agent for a caller that already holds the lifecycle gate.</summary>
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

    private Task HandleNotificationAsync(
        string workspaceId, AgentSessionState state, IAgentProcessProvider process, string method, JsonElement payload)
    {
        var update = payload.TryGetProperty("update", out var nested) ? nested : payload;
        // A replaced process can still emit a title for the session it used to serve, and
        // writing that onto whatever runs now would retitle a different session.
        if (method == "session/update" && IsCurrent(workspaceId, state, process)
            && update.TryGetProperty("sessionUpdate", out var kind)
            && (kind.GetString()?.Contains("session_info", StringComparison.OrdinalIgnoreCase) == true
                || string.Equals(kind.GetString(), "title", StringComparison.OrdinalIgnoreCase))
            && update.TryGetProperty("title", out var title) && !string.IsNullOrWhiteSpace(title.GetString())
            && workspaces.GetById(workspaceId) is { } workspace)
            SaveSession(workspaceId, state, workspace.Branch, title.GetString()!);
        if (method == "session/update"
            && update.TryGetProperty("sessionUpdate", out var updateKind)
            && (updateKind.GetString()?.Contains("tool", StringComparison.OrdinalIgnoreCase) == true
                || updateKind.GetString()?.Contains("function", StringComparison.OrdinalIgnoreCase) == true))
            state.SawToolCall = true;
        state.TranscriptEvents++;
        events.Publish(workspaceId, method == "session/update" ? "session_update" : method, payload);
        return Task.CompletedTask;
    }

    private static string DrainSteering(AgentSessionState state, string message)
    {
        var extras = new List<string>();
        while (state.Steering.TryDequeue(out var item))
            extras.Add(item);
        if (extras.Count == 0)
            return message;
        return message
               + "\n\nSteering from the operator (advisory; do not rewrite the task):\n"
               + string.Join("\n", extras);
    }

    private async Task<JsonElement> HandleRequestAsync(
        string workspaceId, AgentSessionState state, string method, JsonElement payload, CancellationToken cancellationToken)
    {
        if (method != "session/request_permission") throw new InvalidOperationException($"ACP client method '{method}' is not supported.");
        var optionIds = payloads.PermissionOptionIds(payload);
        if (optionIds.Count == 0) throw new InvalidOperationException("The ACP agent requested permission without valid options.");
        if (state.PermissionPolicy is { } policy)
            return ResolvePolicy(workspaceId, state, payload, optionIds, policy);
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!state.Permissions.TryAdd(requestId, new PendingAgentPermission(optionIds, completion))) throw new InvalidOperationException("Could not register permission request.");
        events.Publish(workspaceId, "permission_request", new { requestId, request = payload });
        try
        {
            var optionId = await completion.Task.WaitAsync(cancellationToken);
            return payloads.Payload(new { outcome = new { outcome = "selected", optionId } });
        }
        finally { state.Permissions.TryRemove(requestId, out _); }
    }

    private JsonElement ResolvePolicy(
        string workspaceId,
        AgentSessionState state,
        JsonElement payload,
        IReadOnlySet<string> optionIds,
        IAgentPermissionPolicy policy)
    {
        var decision = policy.Decide(payload);
        if (decision is null)
        {
            state.LoopBlock = "blocked:policy";
            events.Publish(workspaceId, "permission_decision", new { blocked = true, ruleId = (string?)null });
            var reject = optionIds.FirstOrDefault(id => id.Contains("reject", StringComparison.Ordinal)) ?? optionIds.First();
            return payloads.Payload(new { outcome = new { outcome = "selected", optionId = reject } });
        }

        state.PermissionRuleIds.Add(decision.RuleId);
        events.Publish(workspaceId, "permission_decision", new { blocked = false, ruleId = decision.RuleId, optionId = decision.OptionId });
        return payloads.Payload(new { outcome = new { outcome = "selected", optionId = decision.OptionId } });
    }

    private static string? ReadStopReason(JsonElement? result)
    {
        if (result is not { } payload || payload.ValueKind != JsonValueKind.Object) return null;
        return payload.TryGetProperty("stopReason", out var stop) && stop.ValueKind == JsonValueKind.String
            ? stop.GetString()
            : null;
    }

    private static bool ReadCompleted(JsonElement? result)
    {
        if (result is not { } payload || payload.ValueKind != JsonValueKind.Object) return false;
        return payload.TryGetProperty("completed", out var completed)
               && completed.ValueKind == JsonValueKind.True;
    }

    /// <summary>Whether this session and process are still the ones serving the workspace.</summary>
    private bool IsCurrent(string workspaceId, AgentSessionState state, IAgentProcessProvider process) =>
        _sessions.TryGetValue(workspaceId, out var current)
        && ReferenceEquals(current, state)
        && ReferenceEquals(current.Process, process);

    private void HandleExit(string workspaceId, AgentSessionState state, IAgentProcessProvider process, string? error)
    {
        if (!IsCurrent(workspaceId, state, process)) return;
        state.State = "stopped"; state.Error = error;
        if (error is null)
            logger.LogInformation("Agent for workspace {WorkspaceId} exited.", LogSafeIdentifier.Of(workspaceId));
        else
            logger.LogInformation("Agent for workspace {WorkspaceId} exited with an error.", LogSafeIdentifier.Of(workspaceId));
        var snapshot = Get(workspaceId);
        if (snapshot is not null) events.Publish(workspaceId, "state", snapshot);
    }

    private void HandleWorkspaceRemoved(string workspaceId) => _ = StopRemovedWorkspaceAsync(workspaceId);

    private async Task StopRemovedWorkspaceAsync(string workspaceId)
    {
        try
        {
            await StopAsync(workspaceId, CancellationToken.None);
            sessionRepository?.RemoveWorkspace(workspaceId);
            settings?.Remove(workspaceId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        { logger.LogWarning(exception, "Could not stop the agent for removed workspace {WorkspaceId}.", LogSafeIdentifier.Of(workspaceId)); }
    }

    public async Task<bool> HasAvailableAgentAsync(CancellationToken cancellationToken)
    {
        var descriptors = await DescriptorsAsync(cancellationToken);
        return descriptors.Any(descriptor => descriptor.Available);
    }

    private async Task<IReadOnlyList<AgentDescriptor>> DescriptorsAsync(CancellationToken cancellationToken)
    {
        var enabledAgents = packages?.ListAgents() ?? [];
        var listed = FirstPartyAgents
            .Concat(enabledAgents.Select(agent => agent.Identity.Id))
            .Concat(IncludeDevelopmentAgents ? [AgentUnavailability.ScriptedTestAgent] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var descriptors = new AgentDescriptor[listed.Count];
        for (var index = 0; index < listed.Count; index++)
            descriptors[index] = await DescribeAsync(listed[index], enabledAgents, cancellationToken);
        return descriptors;
    }

    private async Task<AgentDescriptor> DescribeAsync(
        string agentId,
        IReadOnlyList<IAgentCapability> enabledAgents,
        CancellationToken cancellationToken)
    {
        var module = enabledAgents.FirstOrDefault(agent =>
            agent.Identity.Id.Equals(agentId, StringComparison.OrdinalIgnoreCase));
        var packageEnabled = module is not null || packages?.GetEnabled(agentId) is not null;
        var developmentAgent = agentId.Equals(AgentUnavailability.ScriptedTestAgent, StringComparison.OrdinalIgnoreCase);
        var displayName = module?.Identity.DisplayName
            ?? packages?.GetEnabled(agentId)?.DisplayName
            ?? DisplayName(agentId);
        var supportsPermissionRequests = module?.SupportsPermissionRequests ?? true;
        var canRun = module?.CanRun ?? true;
        var available = canRun && await commands.IsAvailableAsync(agentId, cancellationToken);
        if (available)
            return new AgentDescriptor(agentId, true, displayName, supportsPermissionRequests);

        if (!packageEnabled && !developmentAgent)
        {
            return new AgentDescriptor(
                agentId,
                false,
                displayName,
                supportsPermissionRequests,
                AgentUnavailability.CapabilityNotEnabled,
                AgentUnavailability.CapabilityNotEnabledDetail(displayName),
                AgentUnavailability.CapabilityNotEnabledRemedy(agentId));
        }

        return new AgentDescriptor(
            agentId,
            false,
            displayName,
            supportsPermissionRequests,
            AgentUnavailability.ExecutableNotFound,
            AgentUnavailability.ExecutableNotFoundDetail(agentId),
            AgentUnavailability.ExecutableNotFoundRemedy(commands.LaunchCommandName(agentId), agentId));
    }

    private bool IncludeDevelopmentAgents =>
        host is not null && (host.IsDevelopment() || host.IsEnvironment("Testing"));

    private string NormalizeAgentId(string agent)
    {
        var listed = _descriptors.FirstOrDefault(descriptor =>
            descriptor.Agent.Equals(agent, StringComparison.OrdinalIgnoreCase));
        if (listed is not null)
            return listed.Agent;
        return packages?.GetAgent(agent)?.Identity.Id
               ?? FirstPartyAgents.FirstOrDefault(id => id.Equals(agent, StringComparison.OrdinalIgnoreCase))
               ?? agent;
    }

    private const string MissingSessionId = "The ACP agent did not return a session ID.";

    private static readonly string[] FirstPartyAgents = ["codex", "cursor", "claude"];

    private static string DisplayName(string agent) => agent.ToLowerInvariant() switch
    {
        "codex" => "Codex",
        "cursor" => "Cursor",
        "claude" => "Claude",
        AgentUnavailability.ScriptedTestAgent => AgentUnavailability.ScriptedTestAgentDisplayName,
        _ => agent
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
