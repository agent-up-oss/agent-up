using System.Collections.ObjectModel;
using System.Reactive;
using System.Text.Json;
using Avalonia.Threading;
using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

public sealed class AgentChatViewModel : ReactiveObject
{
    private readonly AgentsController _controller;
    private string? _workspaceId, _selectedAgent, _sessionId, _message, _error, _sessionTitle, _mode, _usage, _permissionTitle, _permissionDetail, _hintKind, _hintTool;
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
    public ObservableCollection<AgentOptionViewModel> PermissionOptions { get; } = [];
    public ObservableCollection<AgentOptionViewModel> AuthenticationOptions { get; } = [];
    public string? SelectedAgent { get => _selectedAgent; private set => this.RaiseAndSetIfChanged(ref _selectedAgent, value); }
    public string State { get => _state; private set => this.RaiseAndSetIfChanged(ref _state, value); }
    public string ActivityLabel { get => _activityLabel; private set => this.RaiseAndSetIfChanged(ref _activityLabel, value); }
    public string? SessionTitle { get => _sessionTitle; private set => this.RaiseAndSetIfChanged(ref _sessionTitle, value); }
    public string? Mode { get => _mode; private set => this.RaiseAndSetIfChanged(ref _mode, value); }
    public string? Usage { get => _usage; private set => this.RaiseAndSetIfChanged(ref _usage, value); }
    public string? PermissionTitle { get => _permissionTitle; private set => this.RaiseAndSetIfChanged(ref _permissionTitle, value); }
    public string? PermissionDetail { get => _permissionDetail; private set => this.RaiseAndSetIfChanged(ref _permissionDetail, value); }
    public string? ContextLine => string.Join("   ", new[] { SessionTitle, string.IsNullOrWhiteSpace(Mode) ? null : $"Mode · {Mode}", Usage }.Where(part => !string.IsNullOrWhiteSpace(part)));
    public string? Message { get => _message; set => this.RaiseAndSetIfChanged(ref _message, value); }
    public string? Error { get => _error; private set => this.RaiseAndSetIfChanged(ref _error, value); }
    public bool IsVisible { get => _isVisible; set => this.RaiseAndSetIfChanged(ref _isVisible, value); }
    public bool IsBusy { get => _isBusy; private set => this.RaiseAndSetIfChanged(ref _isBusy, value); }
    public bool HasAgent => SelectedAgent is not null;
    public bool HasSession => _sessionId is not null;
    public bool HasPermission => PermissionOptions.Count > 0;
    public bool HasContext => !string.IsNullOrWhiteSpace(ContextLine);
    public bool HasMessages => Transcript.Count > 0;
    public string? SelectedAgentDisplayName =>
        Agents.FirstOrDefault(agent => string.Equals(agent.Agent, SelectedAgent, StringComparison.Ordinal))?.DisplayName
        ?? SelectedAgent;
    public string StatusLine => HasAgent
        ? $"{SelectedAgentDisplayName} · {ActivityLabel}"
        : ActivityLabel;
    public ReactiveCommand<string, Unit> SelectAgentCommand { get; }
    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    public AgentChatViewModel(AgentsController controller)
    {
        _controller = controller;
        SelectAgentCommand = ReactiveCommand.CreateFromTask<string>(SelectAsync);
        SendCommand = ReactiveCommand.CreateFromTask(SendAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopAsync);
        Transcript.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasMessages));
    }

    public async Task LoadAsync(string? workspaceId)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        await StopStreamAsync();
        if (generation != _loadGeneration)
            return;

        _workspaceId = workspaceId;
        _lastSequence = 0;
        Transcript.Clear();
        _currentRun = null;
        PermissionOptions.Clear(); AuthenticationOptions.Clear(); SelectedAgent = null; State = "idle"; Error = null; Agents.Clear();
        SessionTitle = Mode = Usage = PermissionTitle = PermissionDetail = _hintKind = _hintTool = null;
        NotifyComputedChatState();
        RefreshActivity();
        if (workspaceId is null) return;
        try
        {
            var session = await _controller.GetAsync(workspaceId, CancellationToken.None);
            if (generation != _loadGeneration) return;
            Apply(session);
            StartStream(workspaceId);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
        {
            if (generation == _loadGeneration)
                Error = exception.Message;
        }
    }

    private async Task SelectAsync(string agent) => await RunAsync(async id => Apply(await _controller.ScheduleAsync(id, agent, CancellationToken.None)));
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
            if (session.Agents.Count > 0)
            {
                Agents.Clear();
                foreach (var agent in session.Agents)
                    Agents.Add(agent);
            }
            if (session.State == "authentication_required")
                foreach (var method in session.AuthMethods ?? []) AuthenticationOptions.Add(new(method.Id, method.Name, ReactiveCommand.CreateFromTask(() => AuthenticateAsync(method.Id))));
            Error = session.Error;
        }
        if (session?.State is not "running") _hintKind = _hintTool = null;
        RefreshActivity();
        this.RaisePropertyChanged(nameof(HasAgent));
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
        this.RaisePropertyChanged(nameof(HasSession));
        this.RaisePropertyChanged(nameof(HasMessages));
        this.RaisePropertyChanged(nameof(SelectedAgentDisplayName));
        this.RaisePropertyChanged(nameof(StatusLine));
        this.RaisePropertyChanged(nameof(ContextLine));
        this.RaisePropertyChanged(nameof(HasContext));
    }
    private static string Text(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
        if (value.ValueKind == JsonValueKind.Array) return string.Join("\n", value.EnumerateArray().Select(Text).Where(text => !string.IsNullOrWhiteSpace(text)));
        if (value.ValueKind != JsonValueKind.Object) return "";
        return value.TryGetProperty("text", out var text) ? Text(text) : value.TryGetProperty("content", out var nested) ? Text(nested) : "";
    }
}
