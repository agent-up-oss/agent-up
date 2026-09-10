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
    private string? _workspaceId, _selectedAgent, _sessionId, _message, _error;
    private string _state = "idle";
    private bool _isVisible, _isBusy;
    private CancellationTokenSource? _stream;
    private long _lastSequence;
    public ObservableCollection<AgentChatItemViewModel> Messages { get; } = [];
    public ObservableCollection<AgentDescriptorDto> Agents { get; } = [];
    public ObservableCollection<AgentOptionViewModel> PermissionOptions { get; } = [];
    public ObservableCollection<AgentOptionViewModel> AuthenticationOptions { get; } = [];
    public string? SelectedAgent { get => _selectedAgent; private set => this.RaiseAndSetIfChanged(ref _selectedAgent, value); }
    public string State { get => _state; private set => this.RaiseAndSetIfChanged(ref _state, value); }
    public string? Message { get => _message; set => this.RaiseAndSetIfChanged(ref _message, value); }
    public string? Error { get => _error; private set => this.RaiseAndSetIfChanged(ref _error, value); }
    public bool IsVisible { get => _isVisible; set => this.RaiseAndSetIfChanged(ref _isVisible, value); }
    public bool IsBusy { get => _isBusy; private set => this.RaiseAndSetIfChanged(ref _isBusy, value); }
    public bool HasAgent => SelectedAgent is not null;
    public bool HasSession => _sessionId is not null;
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
        _stream?.Cancel(); _stream?.Dispose(); _stream = null; _workspaceId = workspaceId; _lastSequence = 0;
        Messages.Clear(); PermissionOptions.Clear(); AuthenticationOptions.Clear(); SelectedAgent = null; State = "idle"; Error = null; Agents.Clear();
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
        SelectedAgent = session?.Agent; _sessionId = session?.SessionId; State = session?.State ?? "idle";
        AuthenticationOptions.Clear();
        if (session is not null)
        {
            Agents.Clear();
            foreach (var agent in session.Agents) Agents.Add(agent);
            if (session.State == "authentication_required")
                foreach (var method in session.AuthMethods ?? []) AuthenticationOptions.Add(new(method.Id, method.Name, ReactiveCommand.CreateFromTask(() => AuthenticateAsync(method.Id))));
            Error = session.Error;
        }
        this.RaisePropertyChanged(nameof(HasAgent));
        this.RaisePropertyChanged(nameof(HasSession));
    }
    private void StartStream(string id)
    {
        _stream = new CancellationTokenSource();
        _ = RunStreamLoopAsync(id, _stream.Token);
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
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                await Dispatcher.UIThread.InvokeAsync(() => Error = exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        }
    }
    private void Accept(AgentEventDto item)
    {
        _lastSequence = item.Sequence;
        if (item.Type == "state") { Apply(item.Payload.Deserialize<AgentSessionDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })); return; }
        if (item.Type == "user_message") { Messages.Add(new("You", Text(item.Payload))); return; }
        if (item.Type == "session_update") { var update = item.Payload.TryGetProperty("update", out var nested) ? nested : item.Payload; Append(Role(update), Text(update)); return; }
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
    private async Task AuthenticateAsync(string methodId) { if (_workspaceId is null) return; await RunAsync(id => _controller.AuthenticateAsync(id, methodId, CancellationToken.None)); }
    private void Append(string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var last = Messages.LastOrDefault();
        if (last?.Role == role && role is "Agent" or "Thought") Messages[^1] = last with { Text = last.Text + text };
        else Messages.Add(new(role, text));
    }
    private static string Role(JsonElement value)
    {
        var kind = value.TryGetProperty("sessionUpdate", out var update) ? update.GetString() ?? "" : "";
        if (kind.Contains("thought", StringComparison.Ordinal)) return "Thought";
        if (kind.Contains("tool", StringComparison.Ordinal)) return "Tool";
        return kind.Contains("message", StringComparison.Ordinal) ? "Agent" : "Status";
    }
    private static string Text(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
        if (value.ValueKind == JsonValueKind.Array) return string.Join("\n", value.EnumerateArray().Select(Text).Where(text => !string.IsNullOrWhiteSpace(text)));
        if (value.ValueKind != JsonValueKind.Object) return "";
        var title = value.TryGetProperty("title", out var titleValue) ? titleValue.GetString() : null;
        var content = value.TryGetProperty("text", out var text) ? text.GetString()
            : value.TryGetProperty("content", out var nested) ? Text(nested)
            : value.TryGetProperty("entries", out var entries) ? Text(entries) : null;
        var status = value.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
        return string.Join(" · ", new[] { title, content, status }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!));
    }
}
