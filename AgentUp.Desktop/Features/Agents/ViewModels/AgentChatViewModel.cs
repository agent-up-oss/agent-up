using System.Collections.ObjectModel;
using System.Reactive;
using System.Text.Json;
using Avalonia.Threading;
using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

public sealed class AgentChatViewModel : ReactiveObject
{
    private readonly AgentsController _controller;
    private string? _workspaceId, _selectedAgent, _state = "idle", _message, _error;
    private bool _isVisible, _isBusy;
    private CancellationTokenSource? _stream;
    private long _lastSequence;
    public ObservableCollection<AgentChatItemViewModel> Messages { get; } = [];
    public ObservableCollection<AgentDescriptorDto> Agents { get; } = [];
    public ObservableCollection<AgentOptionViewModel> PermissionOptions { get; } = [];
    public string? SelectedAgent { get => _selectedAgent; private set => this.RaiseAndSetIfChanged(ref _selectedAgent, value); }
    public string State { get => _state; private set => this.RaiseAndSetIfChanged(ref _state, value); }
    public string? Message { get => _message; set => this.RaiseAndSetIfChanged(ref _message, value); }
    public string? Error { get => _error; private set => this.RaiseAndSetIfChanged(ref _error, value); }
    public bool IsVisible { get => _isVisible; set => this.RaiseAndSetIfChanged(ref _isVisible, value); }
    public bool IsBusy { get => _isBusy; private set => this.RaiseAndSetIfChanged(ref _isBusy, value); }
    public bool HasAgent => SelectedAgent is not null;
    public bool HasPermission => PermissionOptions.Count > 0;
    public ReactiveCommand<string, Unit> SelectAgentCommand { get; }
    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    public AgentChatViewModel(AgentsController controller)
    {
        _controller = controller;
        SelectAgentCommand = ReactiveCommand.CreateFromTask<string>(SelectAsync);
        SendCommand = ReactiveCommand.CreateFromTask(SendAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopAsync);
    }

    public async Task LoadAsync(string? workspaceId)
    {
        _stream?.Cancel(); _stream?.Dispose(); _stream = null; _workspaceId = workspaceId;
        Messages.Clear(); PermissionOptions.Clear(); SelectedAgent = null; State = "idle"; Error = null; Agents.Clear();
        if (workspaceId is null) return;
        try { Apply(await _controller.GetAsync(workspaceId, CancellationToken.None)); StartStream(workspaceId); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException) { Error = exception.Message; }
    }

    private async Task SelectAsync(string agent) => await RunAsync(async id => Apply(await _controller.ScheduleAsync(id, agent, CancellationToken.None)));
    private async Task SendAsync()
    {
        var text = Message?.Trim(); if (string.IsNullOrEmpty(text)) return;
        Message = null; await RunAsync(id => _controller.SendAsync(id, text, CancellationToken.None));
    }
    private async Task StopAsync() => await RunAsync(async id => { await _controller.StopAsync(id, CancellationToken.None); Apply(null); });
    private async Task RunAsync(Func<string, Task> action)
    {
        if (_workspaceId is null || IsBusy) return; IsBusy = true; Error = null;
        try { await action(_workspaceId); } catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException) { Error = exception.Message; }
        finally { IsBusy = false; }
    }
    private void Apply(AgentSessionDto? session)
    {
        SelectedAgent = session?.Agent; State = session?.State ?? "idle";
        if (session is not null) { Agents.Clear(); foreach (var agent in session.Agents) Agents.Add(agent); Error = session.Error; }
        this.RaisePropertyChanged(nameof(HasAgent));
    }
    private void StartStream(string id)
    {
        _stream = new CancellationTokenSource(); var token = _stream.Token;
        _ = Task.Run(async () => {
            try { await foreach (var item in _controller.EventsAsync(id, _lastSequence, token)) await Dispatcher.UIThread.InvokeAsync(() => Accept(item)); }
            catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException) { if (!token.IsCancellationRequested) await Dispatcher.UIThread.InvokeAsync(() => Error = exception.Message); }
        }, token);
    }
    private void Accept(AgentEventDto item)
    {
        _lastSequence = item.Sequence;
        if (item.Type == "state") { Apply(item.Payload.Deserialize<AgentSessionDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })); return; }
        if (item.Type == "user_message") { Messages.Add(new("You", Text(item.Payload))); return; }
        if (item.Type == "session_update") { var update = item.Payload.TryGetProperty("update", out var nested) ? nested : item.Payload; var text = Text(update); if (!string.IsNullOrEmpty(text)) Messages.Add(new("Agent", text)); return; }
        if (item.Type == "permission_request") AddPermission(item.Payload);
    }
    private void AddPermission(JsonElement payload)
    {
        PermissionOptions.Clear(); var requestId = payload.GetProperty("requestId").GetString()!; var request = payload.GetProperty("request");
        if (!request.TryGetProperty("options", out var options)) return;
        foreach (var option in options.EnumerateArray()) { var id = option.GetProperty("optionId").GetString()!; var label = option.TryGetProperty("name", out var name) ? name.GetString()! : id; PermissionOptions.Add(new(id, label, ReactiveCommand.CreateFromTask(() => DecideAsync(requestId, id)))); }
        this.RaisePropertyChanged(nameof(HasPermission));
    }
    private async Task DecideAsync(string request, string option) { if (_workspaceId is null) return; await _controller.DecideAsync(_workspaceId, request, option, CancellationToken.None); PermissionOptions.Clear(); this.RaisePropertyChanged(nameof(HasPermission)); }
    private static string Text(JsonElement value) { if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? ""; if (value.ValueKind != JsonValueKind.Object) return ""; if (value.TryGetProperty("text", out var text)) return text.GetString() ?? ""; if (value.TryGetProperty("content", out var content)) return Text(content); return ""; }
}
