using System.Reactive;
using AgentUp.Desktop.Features.Agents.Providers;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

public sealed class AgentChatItemViewModel : ReactiveObject
{
    private string _text;
    private string? _status;
    private bool? _expanded;
    private bool _live;
    private bool _toolOpen;
    private bool _showAllOutput;
    private string? _toolTitle;
    private string? _toolKind;
    private string? _rawInput;
    private string? _toolOutput;

    public AgentChatItemViewModel(string role, string text, string? status = null, string? toolCallId = null, string? displayRole = null)
    {
        Role = role;
        _text = text;
        _status = status;
        ToolCallId = toolCallId;
        DisplayRole = displayRole;
        ToggleCommand = ReactiveCommand.Create(Toggle);
        ToggleToolCommand = ReactiveCommand.Create(() => { _toolOpen = !_toolOpen; NotifyTool(); });
        ShowAllOutputCommand = ReactiveCommand.Create(() => { _showAllOutput = true; NotifyTool(); });
    }

    public ReactiveCommand<Unit, Unit> ToggleToolCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAllOutputCommand { get; }
    public bool IsTool => Role == "Tool";

    /// <summary>The agent's own prose: the reply, and anything it said between steps.</summary>
    public bool IsAgentText => Role == "Agent";

    /// <summary>Plan and other steps that are neither a tool call, a thought, nor prose.</summary>
    public bool IsOtherWork => IsWork && !IsTool && !IsAgentText;

    public AgentToolCallView Tool => AgentToolCallProvider.Present(_toolTitle, _toolKind, _rawInput, _toolOutput, Status);
    public string ToolGlyph => Tool.State switch { "done" => "✓", "failed" => "✕", _ => "◐" };
    public bool IsToolRunning => Tool.State == "running";
    public bool IsToolFailed => Tool.State == "failed";
    public bool HasToolSource => Tool.Source is not null;
    public bool HasToolSummary => Tool.Summary is not null;
    public bool IsToolOpen => _toolOpen;
    public string ToolChevron => _toolOpen ? "⌄" : "›";
    public bool HasToolInput => _toolOpen && Tool.Input is not null;
    public bool HasToolOutput => _toolOpen && Tool.Output is not null;
    public string? ToolOutputText => _showAllOutput ? Tool.FullOutput : Tool.Output;
    public bool CanShowAllOutput => _toolOpen && !_showAllOutput && Tool.HiddenLines > 0;
    public string ShowAllOutputLabel => Tool.HiddenLines == 1 ? "Show 1 more line" : $"Show {Tool.HiddenLines} more lines";
    public string ToolAutomationId => DesktopAutomationIds.AgentToolCall(ToolCallId ?? "tool");

    /// <summary>Takes the latest tool fields; an update that omits one keeps what came before.</summary>
    public void SetTool(string? title, string? kind, string? rawInput, string? output, string? status)
    {
        _toolTitle = title ?? _toolTitle;
        _toolKind = kind ?? _toolKind;
        _rawInput = rawInput ?? _rawInput;
        _toolOutput = output ?? _toolOutput;
        if (status is not null) Status = status;
        NotifyTool();
    }

    private void NotifyTool()
    {
        foreach (var name in new[]
                 {
                     nameof(Tool), nameof(ToolGlyph), nameof(IsToolRunning), nameof(IsToolFailed), nameof(HasToolSource),
                     nameof(HasToolSummary), nameof(IsToolOpen), nameof(ToolChevron), nameof(HasToolInput), nameof(HasToolOutput),
                     nameof(ToolOutputText), nameof(CanShowAllOutput), nameof(ShowAllOutputLabel)
                 })
            this.RaisePropertyChanged(name);
    }

    public string Role { get; }
    public string? ToolCallId { get; }
    public string? DisplayRole { get; }
    public string AutomationId => DesktopAutomationIds.AgentThought(ToolCallId ?? Role);
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
