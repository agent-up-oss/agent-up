using System.Collections.ObjectModel;
using System.Reactive;
using System.Text.Json;
using Avalonia;
using Avalonia.Threading;
using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Providers;
using AgentUp.Desktop.Features.Capabilities.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

public sealed class AgentChatViewModel : ReactiveObject
{
    private readonly AgentsController _controller;
    private readonly CapabilityModulesController? _modules;
    private string? _workspaceId, _selectedAgent, _boundAgent, _sessionId, _message, _error, _sessionTitle, _mode, _usage, _permissionTitle, _permissionDetail, _hintKind, _hintTool, _loginUrl, _loginCode, _loginInstructions, _loginCodeInput;
    private bool _canCollectLoginCode, _canSubmitLoginCode;
    private AgentLoopStatusDto? _loop;
    private CancellationTokenSource? _loopPoll;
    private Task? _loopPollLoop;
    private string _state = "idle";
    private string _activityLabel = "Idle";
    private bool _isVisible, _isBusy;
    private AgentRunViewModel? _currentRun;
    private CancellationTokenSource? _stream;
    private Task? _streamLoop;
    private int _loadGeneration;
    private long _lastSequence;
    private Task _stopStream = Task.CompletedTask;
    public ObservableCollection<object> Transcript { get; } = [];
    public IEnumerable<AgentChatItemViewModel> Messages => Transcript.OfType<AgentChatItemViewModel>()
        .Concat(Transcript.OfType<AgentRunViewModel>().SelectMany(run => run.Items));
    public ObservableCollection<AgentDescriptorDto> Agents { get; } = [];
    public ObservableCollection<AgentSessionRowViewModel> Sessions { get; } = [];
    public ObservableCollection<AgentOptionViewModel> PermissionOptions { get; } = [];
    public ObservableCollection<AgentOptionViewModel> AuthenticationOptions { get; } = [];
    public string? SelectedAgent { get => _selectedAgent; private set => this.RaiseAndSetIfChanged(ref _selectedAgent, value); }
    public string? BoundAgent { get => _boundAgent; private set => this.RaiseAndSetIfChanged(ref _boundAgent, value); }
    public AgentLoopStatusDto? Loop { get => _loop; private set => this.RaiseAndSetIfChanged(ref _loop, value); }
    public ObservableCollection<AutonomyPreconditionDto> Preconditions { get; } = [];
    public bool HasAutonomy => Preconditions.Count > 0;
    public bool CanEnableAutonomy =>
        Satisfied("forgeCapabilityEnabled")
        && Satisfied("hostConnectionAuthorized")
        && Satisfied("agentAvailable")
        && Preconditions.Any(item => item.Id == "autonomyEnabled" && !item.Satisfied)
        && !IsBusy;
    public bool CanActivateLoop =>
        HasAutonomy
        && Preconditions.All(item => item.Satisfied)
        && Loop?.Active != true
        && !IsBusy;
    public bool CanCancelLoop => Loop?.Active == true && !IsBusy;
    public string? LoopTerminal => Loop?.Terminal;
    public string? LoopPhase => Loop?.Phase;
    public bool HasLoopTerminal => !string.IsNullOrWhiteSpace(LoopTerminal);
    public bool HasLoopPhase => !string.IsNullOrWhiteSpace(LoopPhase);
    public string State { get => _state; private set => this.RaiseAndSetIfChanged(ref _state, value); }
    public string ActivityLabel { get => _activityLabel; private set => this.RaiseAndSetIfChanged(ref _activityLabel, value); }
    public string? SessionTitle { get => _sessionTitle; private set => this.RaiseAndSetIfChanged(ref _sessionTitle, value); }
    public string? Mode { get => _mode; private set => this.RaiseAndSetIfChanged(ref _mode, value); }
    public string? Usage { get => _usage; private set => this.RaiseAndSetIfChanged(ref _usage, value); }
    public string? PermissionTitle { get => _permissionTitle; private set => this.RaiseAndSetIfChanged(ref _permissionTitle, value); }
    public string? PermissionDetail { get => _permissionDetail; private set => this.RaiseAndSetIfChanged(ref _permissionDetail, value); }
    public string? LoginUrl { get => _loginUrl; private set => this.RaiseAndSetIfChanged(ref _loginUrl, value); }
    public string? LoginCode { get => _loginCode; private set => this.RaiseAndSetIfChanged(ref _loginCode, value); }
    public string? LoginInstructions { get => _loginInstructions; private set => this.RaiseAndSetIfChanged(ref _loginInstructions, value); }
    public string? LoginCodeInput { get => _loginCodeInput; set => this.RaiseAndSetIfChanged(ref _loginCodeInput, value); }
    public bool HasLoginChallenge => !string.IsNullOrWhiteSpace(LoginUrl) || !string.IsNullOrWhiteSpace(LoginCode);
    public bool HasLoginUrl => !string.IsNullOrWhiteSpace(LoginUrl);
    public bool HasLoginCode => !string.IsNullOrWhiteSpace(LoginCode);
    public bool CanCollectLoginCode { get => _canCollectLoginCode; private set => this.RaiseAndSetIfChanged(ref _canCollectLoginCode, value); }
    public bool CanSubmitLoginCode { get => _canSubmitLoginCode; private set => this.RaiseAndSetIfChanged(ref _canSubmitLoginCode, value); }
    public string? ContextLine => string.Join("   ", new[] { SessionTitle, string.IsNullOrWhiteSpace(Mode) ? null : $"Mode · {Mode}", Usage }.Where(part => !string.IsNullOrWhiteSpace(part)));
    public string? Message { get => _message; set => this.RaiseAndSetIfChanged(ref _message, value); }
    public string? Error { get => _error; private set => this.RaiseAndSetIfChanged(ref _error, value); }
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isVisible, value);
            if (value)
                TryStartLoopPoll();
            else
                _ = StopLoopPollAsync();
        }
    }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBusy, value);
            NotifyLoopSurface();
        }
    }
    public bool HasAgent => SelectedAgent is not null;
    public bool HasBoundAgent => BoundAgent is not null;
    public bool CanStartBound => HasBoundAgent && !HasAgent;
    public string? SessionId => _sessionId;
    public bool HasSession => _sessionId is not null;
    public bool HasNoSavedSessions => Sessions.Count == 0;
    public bool HasPermission => PermissionOptions.Count > 0;
    public bool HasContext => !string.IsNullOrWhiteSpace(ContextLine);
    public bool HasMessages => Transcript.Count > 0;
    public string? SelectedAgentDisplayName =>
        Agents.FirstOrDefault(agent => string.Equals(agent.Agent, SelectedAgent, StringComparison.Ordinal))?.DisplayName
        ?? SelectedAgent;
    public string? BoundAgentDisplayName =>
        Agents.FirstOrDefault(agent => string.Equals(agent.Agent, BoundAgent, StringComparison.Ordinal))?.DisplayName
        ?? BoundAgent;
    public string? LoopLine => Loop is null
        ? null
        : string.Join(" · ", new[] { Loop.Phase, Loop.TaskTitle, Loop.Terminal, Loop.StopReason }.Where(part => !string.IsNullOrWhiteSpace(part)));
    public bool HasLoop => !string.IsNullOrWhiteSpace(LoopLine);
    public string StatusLine => HasAgent
        ? $"{SelectedAgentDisplayName} · {ActivityLabel}"
        : HasBoundAgent
            ? $"{BoundAgentDisplayName} · {ActivityLabel}"
            : ActivityLabel;
    public ReactiveCommand<string, Unit> SelectAgentCommand { get; }
    public ReactiveCommand<string, Unit> EnableAgentCommand { get; }
    public ReactiveCommand<Unit, Unit> StartBoundCommand { get; }
    public ReactiveCommand<string, Unit> ResumeSessionCommand { get; }
    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> SubmitLoginCodeCommand { get; }
    public ReactiveCommand<Unit, Unit> EnableAutonomyCommand { get; }
    public ReactiveCommand<Unit, Unit> ActivateLoopCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelLoopCommand { get; }

    public AgentChatViewModel(AgentsController controller, CapabilityModulesController? modules = null)
    {
        _controller = controller;
        _modules = modules;
        SelectAgentCommand = ReactiveCommand.CreateFromTask<string>(SelectAsync);
        EnableAgentCommand = ReactiveCommand.CreateFromTask<string>(EnableAgentAsync);
        StartBoundCommand = ReactiveCommand.CreateFromTask(StartBoundAsync);
        ResumeSessionCommand = ReactiveCommand.CreateFromTask<string>(ResumeAsync);
        SendCommand = ReactiveCommand.CreateFromTask(SendAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopAsync);
        SubmitLoginCodeCommand = ReactiveCommand.CreateFromTask(SubmitLoginCodeAsync);
        EnableAutonomyCommand = ReactiveCommand.CreateFromTask(EnableAutonomyAsync);
        ActivateLoopCommand = ReactiveCommand.CreateFromTask(ActivateLoopAsync);
        CancelLoopCommand = ReactiveCommand.CreateFromTask(CancelLoopAsync);
        Transcript.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasMessages));
        Sessions.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasNoSavedSessions));
        Preconditions.CollectionChanged += (_, _) => NotifyLoopSurface();
    }

    public async Task LoadAsync(string? workspaceId)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        await StopStreamAsync();
        if (generation != _loadGeneration)
            return;

        _workspaceId = workspaceId;
        _lastSequence = 0;
        await StopLoopPollAsync();
        Transcript.Clear();
        _currentRun = null;
        PermissionOptions.Clear(); AuthenticationOptions.Clear(); SelectedAgent = null; BoundAgent = null; Loop = null; State = "idle"; Error = null; Agents.Clear(); Sessions.Clear(); Preconditions.Clear();
        SessionTitle = Mode = Usage = PermissionTitle = PermissionDetail = LoginUrl = LoginCode = LoginInstructions = LoginCodeInput = _hintKind = _hintTool = null;
        CanCollectLoginCode = CanSubmitLoginCode = false;
        NotifyComputedChatState();
        RefreshActivity();
        if (workspaceId is null) return;
        try
        {
            var session = await _controller.GetAsync(workspaceId, CancellationToken.None);
            if (generation != _loadGeneration) return;
            Apply(session);
            await LoadAutonomyAsync(generation);
            if (generation != _loadGeneration) return;
            StartStream(workspaceId);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
        {
            if (generation == _loadGeneration)
                Error = exception.Message;
        }
    }

    private async Task SelectAsync(string agent) => await RunAsync(async id =>
    {
        Apply(await _controller.BindAsync(id, agent, CancellationToken.None));
        Apply(await _controller.ScheduleAsync(id, agent, CancellationToken.None));
    });
    private async Task EnableAgentAsync(string agent)
    {
        if (_modules is null)
        {
            Error = "This Server cannot enable capability packages.";
            return;
        }

        await RunAsync(async id =>
        {
            await _modules.EnableAsync(agent, version: null);
            Apply(await _controller.GetAsync(id, CancellationToken.None));
        });
    }
    private async Task StartBoundAsync() => await RunAsync(async id =>
    {
        if (BoundAgent is null) return;
        Apply(await _controller.ScheduleAsync(id, BoundAgent, CancellationToken.None));
    });
    private async Task ResumeAsync(string sessionId) => await RunAsync(async id => Apply(await _controller.ResumeAsync(id, sessionId, CancellationToken.None)));
    private async Task SendAsync()
    {
        var text = Message?.Trim(); if (string.IsNullOrEmpty(text) || HasPermission) return;
        Message = null; await RunAsync(id => _controller.SendAsync(id, text, CancellationToken.None));
    }
    private async Task StopAsync() => await RunAsync(async id =>
    {
        await _controller.StopAsync(id, CancellationToken.None);
        Transcript.Clear();
        _currentRun = null;
        Apply(null);
    });
    private async Task EnableAutonomyAsync() => await RunAsync(async _ =>
    {
        await _controller.PutAutonomyAsync(true, CancellationToken.None);
        ApplyAutonomy(await _controller.GetAutonomyAsync(CancellationToken.None));
    });
    private async Task ActivateLoopAsync() => await RunAsync(async id =>
    {
        await _controller.PutLoopActivationAsync(id, true, CancellationToken.None);
        ApplyLoop(await _controller.GetLoopAsync(id, CancellationToken.None));
        ApplyAutonomy(await _controller.GetAutonomyAsync(CancellationToken.None));
        TryStartLoopPoll();
    });
    private async Task CancelLoopAsync() => await RunAsync(async id =>
    {
        await _controller.PutLoopActivationAsync(id, false, CancellationToken.None);
        await StopLoopPollAsync();
        ApplyLoop(await _controller.GetLoopAsync(id, CancellationToken.None));
    });
    private async Task RunAsync(Func<string, Task> action)
    {
        if (_workspaceId is null || IsBusy) return; IsBusy = true; Error = null;
        try { await action(_workspaceId); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
        { Error = exception.Message; }
        finally { IsBusy = false; }
    }
    private void Apply(AgentSessionDto? session)
    {
        SelectedAgent = session?.Agent; _sessionId = session?.SessionId; State = session?.State ?? "idle";
        AuthenticationOptions.Clear();
        if (session is not null)
        {
            BoundAgent = session.BoundAgent;
            Loop = session.Loop;
            Sessions.Clear();
            foreach (var saved in session.Sessions ?? [])
                Sessions.Add(new AgentSessionRowViewModel(saved, IsCurrentSession(saved, session), session.State));
            if (session.Agents.Count > 0)
            {
                Agents.Clear();
                foreach (var agent in session.Agents)
                    Agents.Add(agent);
            }
            if (session.State == "authentication_required")
                foreach (var method in session.AuthMethods ?? []) AuthenticationOptions.Add(new(method.Id, method.Name, ReactiveCommand.CreateFromTask(() => AuthenticateAsync(method.Id))));
            Error = IsExpectedAuthenticationNotice(session.State, session.Error) ? null : session.Error;
            LoginUrl = session.LoginChallenge?.Url;
            LoginCode = session.LoginChallenge?.Code;
            LoginInstructions = session.LoginChallenge?.Instructions;
            CanCollectLoginCode = session.State == "authenticating"
                && session.LoginChallenge?.Transport == AgentLoginTransport.Code
                && string.IsNullOrWhiteSpace(session.LoginChallenge.Code)
                && session.LoginChallenge.CanSubmitCode;
            CanSubmitLoginCode = CanCollectLoginCode;
        }
        else
        {
            LoginUrl = LoginCode = LoginInstructions = null;
            CanCollectLoginCode = CanSubmitLoginCode = false;
        }
        if (session?.State is not "running") _hintKind = _hintTool = null;
        RefreshActivity();
        this.RaisePropertyChanged(nameof(HasAgent));
        this.RaisePropertyChanged(nameof(HasBoundAgent));
        this.RaisePropertyChanged(nameof(CanStartBound));
        this.RaisePropertyChanged(nameof(HasSession));
        NotifyComputedChatState();
    }
    private void StartStream(string id)
    {
        _stream = new CancellationTokenSource();
        _streamLoop = RunStreamLoopAsync(id, _stream.Token);
    }
    private async Task RunStreamLoopAsync(string id, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var item in _controller.EventsAsync(id, _lastSequence, cancellationToken))
                    await Dispatcher.UIThread.InvokeAsync(() => Accept(item));
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (TaskCanceledException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
            {
                await Dispatcher.UIThread.InvokeAsync(() => Error = exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }
    private void Accept(AgentEventDto item)
    {
        if (_stream is null || _stream.IsCancellationRequested)
            return;
        _lastSequence = item.Sequence;
        if (item.Type == "state") { Apply(item.Payload.Deserialize<AgentSessionDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })); return; }
        if (item.Type == "user_message") { AddUser(Text(item.Payload)); return; }
        if (item.Type == "permission_request") { AddPermission(item.Payload); return; }
        if (item.Type != "session_update") return;
        ApplyPresented(AgentEventPresentationProvider.Present(AgentEventPresentationProvider.Unwrap(item.Payload)));
    }
    private void ApplyPresented(PresentedAgentUpdate presented)
    {
        if (presented.Kind == "ignore") return;
        if (presented.Kind == "context")
        {
            if (presented.Title is not null) SessionTitle = presented.Title;
            if (presented.Mode is not null) Mode = presented.Mode;
            if (presented.Usage is not null) Usage = presented.Usage;
            this.RaisePropertyChanged(nameof(ContextLine));
            this.RaisePropertyChanged(nameof(HasContext));
            if (presented.Compacting == true) { _hintKind = "Compacting"; RefreshActivity(); }
            else if (presented.Compacting == false && _hintKind == "Compacting") { _hintKind = null; RefreshActivity(); }
            return;
        }
        if (presented.Kind == "tool") { UpsertTool(presented); _hintKind = "Tool"; _hintTool = presented.Title ?? _hintTool; RefreshActivity(); return; }
        if (presented.Kind == "plan")
        {
            var text = presented.Text ?? "";
            var existing = CurrentRun().Items.ToList().FindIndex(item => item.Role == "Plan");
            if (existing < 0) CurrentRun().Items.Add(new("Plan", text));
            else CurrentRun().Items[existing].Text = text;
            _hintKind = "Plan"; RefreshActivity(); return;
        }
        if (presented.Kind == "message" && !string.IsNullOrWhiteSpace(presented.Text) && presented.Role is not null)
        {
            Append(presented.Role, presented.Text);
            _hintKind = presented.Role;
            RefreshActivity();
        }
    }
    private void UpsertTool(PresentedAgentUpdate presented)
    {
        var run = CurrentRun();
        var existing = run.Items.ToList().FindIndex(item => item.Role == "Tool" && item.ToolCallId == presented.ToolCallId);
        var previous = existing >= 0 ? run.Items[existing] : null;
        var title = presented.Title ?? previous?.Text.Split('\n')[0] ?? "Tool";
        var status = presented.Status ?? previous?.Status ?? "pending";
        var body = presented.Text ?? (previous is null ? "" : string.Join('\n', previous.Text.Split('\n').Skip(1)));
        var text = string.IsNullOrWhiteSpace(body) ? title : $"{title}\n{body}";
        if (previous is null)
            run.Items.Add(new("Tool", text, status, presented.ToolCallId));
        else
        {
            previous.Text = text;
            previous.Status = status;
        }
    }
    private void AddPermission(JsonElement payload)
    {
        PermissionOptions.Clear();
        var prompt = AgentEventPresentationProvider.ParsePermission(payload);
        PermissionTitle = prompt?.Title;
        PermissionDetail = prompt?.Detail;
        if (prompt is null) { this.RaisePropertyChanged(nameof(HasPermission)); RefreshActivity(); return; }
        foreach (var option in prompt.Options)
        {
            var label = AgentEventPresentationProvider.OptionLabel(option.Name, option.Kind, option.OptionId);
            PermissionOptions.Add(new(option.OptionId, label, ReactiveCommand.CreateFromTask(() => DecideAsync(prompt.RequestId, option.OptionId))));
        }
        this.RaisePropertyChanged(nameof(HasPermission));
        RefreshActivity();
    }
    private async Task DecideAsync(string request, string option)
    {
        await RunAsync(async id =>
        {
            await _controller.DecideAsync(id, request, option, CancellationToken.None);
            PermissionOptions.Clear();
            PermissionTitle = PermissionDetail = null;
            NotifyComputedChatState();
            RefreshActivity();
        });
    }
    private async Task AuthenticateAsync(string methodId) { if (_workspaceId is null) return; await RunAsync(id => _controller.AuthenticateAsync(id, methodId, CancellationToken.None)); }
    private async Task SubmitLoginCodeAsync()
    {
        var code = LoginCodeInput?.Trim();
        if (string.IsNullOrEmpty(code) || !CanSubmitLoginCode) return;
        LoginCodeInput = null;
        await RunAsync(id => _controller.SubmitLoginCodeAsync(id, code, CancellationToken.None));
    }

    private static bool IsCurrentSession(AgentSessionSummaryDto saved, AgentSessionDto session) =>
        string.Equals(saved.SessionId, session.SessionId, StringComparison.Ordinal)
        && session.State is not "stopped";

    private static bool IsExpectedAuthenticationNotice(string state, string? error)
    {
        if (state is not ("authentication_required" or "authenticating")) return false;
        return string.IsNullOrWhiteSpace(error)
            || error.Contains("Authentication required", StringComparison.OrdinalIgnoreCase)
            || error.Contains("-32000", StringComparison.Ordinal);
    }
    private void Append(string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var run = CurrentRun();
        var last = run.Items.LastOrDefault();
        if (last is { } item && item.Role == role && role is "Agent" or "Thought")
            item.Text += text;
        else
            run.Items.Add(new(role, text, displayRole: role == "Agent" ? SelectedAgentDisplayName : null));
    }
    private void RefreshActivity()
    {
        ActivityLabel = AgentEventPresentationProvider.ActivityLabel(State, HasPermission, null, _hintKind, _hintTool);
        this.RaisePropertyChanged(nameof(StatusLine));
        RefreshThoughts();
    }

    private void RefreshThoughts()
    {
        var run = _currentRun;
        if (run is null)
            return;
        for (var i = 0; i < run.WorkItems.Count; i++)
        {
            var item = run.WorkItems[i];
            if (!item.IsThought)
                continue;
            item.SetLive(run.IsLive && i == run.WorkItems.Count - 1 && State == "running" && _hintKind == "Thought");
        }
    }

    private void AddUser(string text)
    {
        SealCurrentRun();
        Transcript.Add(new AgentChatItemViewModel("You", text));
    }

    private AgentRunViewModel CurrentRun()
    {
        if (_currentRun is not null)
            return _currentRun;
        _currentRun = new AgentRunViewModel();
        Transcript.Add(_currentRun);
        return _currentRun;
    }

    private void SealCurrentRun()
    {
        if (_currentRun is null)
            return;
        if (_currentRun.Items.Count == 0)
            Transcript.Remove(_currentRun);
        else
            _currentRun.Seal();
        _currentRun = null;
    }

    private Task StopStreamAsync()
    {
        _stopStream = StopCapturedStreamAsync(_stopStream);
        return _stopStream;
    }

    private async Task StopCapturedStreamAsync(Task previous)
    {
        await previous;
        var stream = _stream;
        var loop = _streamLoop;
        _stream = null;
        _streamLoop = null;
        if (stream is null)
            return;

        await stream.CancelAsync();
        if (loop is not null)
        {
            try { await loop; }
            catch (OperationCanceledException)
            {
                loop = null;
            }
        }

        stream.Dispose();
    }

    private void NotifyComputedChatState()
    {
        this.RaisePropertyChanged(nameof(HasPermission));
        this.RaisePropertyChanged(nameof(HasAgent));
        this.RaisePropertyChanged(nameof(HasBoundAgent));
        this.RaisePropertyChanged(nameof(CanStartBound));
        this.RaisePropertyChanged(nameof(HasSession));
        this.RaisePropertyChanged(nameof(HasMessages));
        this.RaisePropertyChanged(nameof(SelectedAgentDisplayName));
        this.RaisePropertyChanged(nameof(BoundAgentDisplayName));
        this.RaisePropertyChanged(nameof(LoopLine));
        this.RaisePropertyChanged(nameof(HasLoop));
        NotifyLoopSurface();
        this.RaisePropertyChanged(nameof(StatusLine));
        this.RaisePropertyChanged(nameof(ContextLine));
        this.RaisePropertyChanged(nameof(HasContext));
        this.RaisePropertyChanged(nameof(HasLoginChallenge));
        this.RaisePropertyChanged(nameof(HasLoginUrl));
        this.RaisePropertyChanged(nameof(HasLoginCode));
        this.RaisePropertyChanged(nameof(HasNoSavedSessions));
        this.RaisePropertyChanged(nameof(CanCollectLoginCode));
        this.RaisePropertyChanged(nameof(CanSubmitLoginCode));
        NotifyLoopSurface();
    }

    private bool Satisfied(string id) => Preconditions.Any(item => item.Id == id && item.Satisfied);

    private async Task LoadAutonomyAsync(int generation)
    {
        try
        {
            var autonomy = await _controller.GetAutonomyAsync(CancellationToken.None);
            if (generation != _loadGeneration)
                return;
            ApplyAutonomy(autonomy);
            TryStartLoopPoll();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
        {
            if (generation == _loadGeneration && Error is null)
                Error = exception.Message;
        }
    }

    private void ApplyAutonomy(ServerAutonomyDto? autonomy)
    {
        Preconditions.Clear();
        foreach (var item in autonomy?.Preconditions ?? [])
            Preconditions.Add(item);
        NotifyLoopSurface();
    }

    private void ApplyLoop(AgentLoopStatusDto? loop)
    {
        Loop = loop;
        NotifyLoopSurface();
    }

    private void NotifyLoopSurface()
    {
        this.RaisePropertyChanged(nameof(HasAutonomy));
        this.RaisePropertyChanged(nameof(CanEnableAutonomy));
        this.RaisePropertyChanged(nameof(CanActivateLoop));
        this.RaisePropertyChanged(nameof(CanCancelLoop));
        this.RaisePropertyChanged(nameof(LoopTerminal));
        this.RaisePropertyChanged(nameof(LoopPhase));
        this.RaisePropertyChanged(nameof(HasLoopTerminal));
        this.RaisePropertyChanged(nameof(HasLoopPhase));
        this.RaisePropertyChanged(nameof(LoopLine));
        this.RaisePropertyChanged(nameof(HasLoop));
    }

    private void TryStartLoopPoll()
    {
        if (!IsVisible || Application.Current is null || Loop?.Active != true || _workspaceId is null)
            return;
        _loopPoll?.Cancel();
        _loopPoll = new CancellationTokenSource();
        _loopPollLoop = PollLoopAsync(_workspaceId, _loopPoll.Token);
    }

    private async Task PollLoopAsync(string id, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var loop = await _controller.GetLoopAsync(id, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return;
                Dispatch(() => ApplyLoop(loop));
                if (loop?.Active != true)
                    return;
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private static void Dispatch(Action action)
    {
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private Task StopLoopPollAsync()
    {
        var poll = _loopPoll;
        var loop = _loopPollLoop;
        _loopPoll = null;
        _loopPollLoop = null;
        if (poll is null)
            return Task.CompletedTask;
        return StopCapturedLoopPollAsync(poll, loop);
    }

    private static async Task StopCapturedLoopPollAsync(CancellationTokenSource poll, Task? loop)
    {
        await poll.CancelAsync();
        if (loop is not null)
        {
            try { await loop; }
            catch (OperationCanceledException)
            {
                loop = null;
            }
        }

        poll.Dispose();
    }
    private static string Text(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
        if (value.ValueKind == JsonValueKind.Array) return string.Join("\n", value.EnumerateArray().Select(Text).Where(text => !string.IsNullOrWhiteSpace(text)));
        if (value.ValueKind != JsonValueKind.Object) return "";
        return value.TryGetProperty("text", out var text) ? Text(text) : value.TryGetProperty("content", out var nested) ? Text(nested) : "";
    }
}
