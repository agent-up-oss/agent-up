using System.Reactive;
using AgentUp.Desktop.Features.Agents.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

public sealed class AgentChatItemViewModel : ReactiveObject
{
    private string _text;
    private string? _status;
    private bool? _expanded;
    private bool _live;

    public AgentChatItemViewModel(string role, string text, string? status = null, string? toolCallId = null, string? displayRole = null)
    {
        Role = role;
        _text = text;
        _status = status;
        ToolCallId = toolCallId;
        DisplayRole = displayRole;
        ToggleCommand = ReactiveCommand.Create(Toggle);
    }

    public string Role { get; }
    public string? ToolCallId { get; }
    public string? DisplayRole { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;
            this.RaiseAndSetIfChanged(ref _text, value);
            this.RaisePropertyChanged(nameof(VisibleText));
        }
    }
    public string? Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }
    public bool IsThought => Role == "Thought";
    public bool IsUser => Role == "You";
    public bool IsWork => !IsThought && !IsUser;
    public string VisibleText => AgentEventPresentationProvider.VisibleText(Text);
    public bool IsThoughtExpanded => _expanded ?? _live;
    public string Label => IsThought
        ? (_live ? "Thinking" : "Thought")
        : DisplayRole ?? Role;

    public void SetLive(bool live)
    {
        if (_live == live)
            return;
        _live = live;
        NotifyThought();
    }

    private void Toggle()
    {
        _expanded = !IsThoughtExpanded;
        NotifyThought();
    }

    private void NotifyThought()
    {
        this.RaisePropertyChanged(nameof(IsThoughtExpanded));
        this.RaisePropertyChanged(nameof(Label));
    }
}
