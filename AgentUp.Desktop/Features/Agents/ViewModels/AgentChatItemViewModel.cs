using System.Reactive;
using AgentUp.Desktop.Features.Agents.Models;
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
    private bool _showWholeDiff;
    private AgentToolCallFacts _facts = new(null, null, null, null, null, null);
    private DateTimeOffset? _firstAt;
    private DateTimeOffset? _lastAt;

    /// <summary>Diff lines shown before the rest of a long edit is folded.</summary>
    public const int DiffPreviewLines = 40;

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
        ShowWholeDiffCommand = ReactiveCommand.Create(() => { _showWholeDiff = true; NotifyTool(); });
    }

    public ReactiveCommand<Unit, Unit> ToggleToolCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAllOutputCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowWholeDiffCommand { get; }
    public bool IsTool => Role == "Tool";

    /// <summary>The agent's own prose: the reply, and anything it said between steps.</summary>
    public bool IsAgentText => Role == "Agent";

    /// <summary>Plan and other steps that are neither a tool call, a thought, nor prose.</summary>
    public bool IsOtherWork => IsWork && !IsTool && !IsAgentText;

    public AgentToolCallView Tool => AgentToolCallProvider.Present(_facts with { Status = Status, StartedAt = _firstAt, EndedAt = Status is "completed" or "failed" ? _lastAt : null });

    /// <summary>The tool's own name as the agent first gave it, which later updates may retitle.</summary>
    public string? ToolName => _facts.Name;

    /// <summary>The ACP kind the agent gave the call, kept across updates that omit it.</summary>
    public string? ToolKind => _facts.Kind;
    public bool HasToolDiff => Tool.Diff is { Lines.Count: > 0 };
    public bool HasToolDiffOpen => _toolOpen && HasToolDiff;
    public string? DiffAdded => Tool.Diff is { Added: > 0 } diff ? $"+{diff.Added}" : null;
    public string? DiffRemoved => Tool.Diff is { Removed: > 0 } diff ? $"−{diff.Removed}" : null;
    public bool HasDiffAdded => DiffAdded is not null;
    public bool HasDiffRemoved => DiffRemoved is not null;
    public IReadOnlyList<AgentDiffLineViewModel> DiffLines => (Tool.Diff?.Lines ?? [])
        .Take(_showWholeDiff ? int.MaxValue : DiffPreviewLines)
        .Select(line => new AgentDiffLineViewModel(line, Tool.Diff?.Path))
        .ToArray();
    public bool CanShowWholeDiff => HasToolDiffOpen && !_showWholeDiff && (Tool.Diff?.Lines.Count ?? 0) > DiffPreviewLines;
    public string ShowWholeDiffLabel => $"Show all {Tool.Diff?.Lines.Count ?? 0} lines";
    public string? ToolDuration => Tool.Duration;
    public bool HasToolDuration => ToolDuration is not null;
    public string ToolGlyph => Tool.State switch { "done" => "✓", "failed" => "✕", _ => "◐" };
    public bool IsToolRunning => Tool.State == "running";
    public bool IsToolFailed => Tool.State == "failed";
    public bool HasToolSource => Tool.Source is not null;
    public bool HasToolSummary => Tool.Summary is not null;
    public bool IsToolOpen => _toolOpen;
    public string ToolChevron => _toolOpen ? "⌄" : "›";
    // An edit's diff already says what its input asked for, so the raw input is not repeated under it.
    public bool HasToolInput => _toolOpen && Tool.Input is not null && !HasToolDiff;
    public bool HasToolOutput => _toolOpen && Tool.Output is not null && !HasToolDiff;
    public string? ToolOutputText => _showAllOutput ? Tool.FullOutput : Tool.Output;
    public bool CanShowAllOutput => _toolOpen && !_showAllOutput && Tool.HiddenLines > 0;
    public string ShowAllOutputLabel => Tool.HiddenLines == 1 ? "Show 1 more line" : $"Show {Tool.HiddenLines} more lines";
    public string ToolAutomationId => DesktopAutomationIds.AgentToolCall(ToolCallId ?? "tool");

    /// <summary>Takes the latest tool fields; an update that omits one keeps what came before.</summary>
    public void SetTool(string? title, string? kind, string? rawInput, string? output, string? status)
        => SetTool(new PresentedAgentUpdate("tool", Title: title, ToolKind: kind, RawInput: rawInput, Text: output, Status: status), null);

    /// <summary>
    /// Folds one tool update into the call: every field an update omits keeps its earlier value,
    /// and the first title is kept as the tool's name.
    /// </summary>
    public void SetTool(PresentedAgentUpdate update, DateTimeOffset? at)
    {
        _facts = _facts with
        {
            Name = _facts.Name ?? update.Title,
            Title = update.Title ?? _facts.Title,
            Kind = update.ToolKind ?? _facts.Kind,
            RawInput = update.RawInput ?? _facts.RawInput,
            Output = string.IsNullOrEmpty(update.Text) ? _facts.Output ?? update.Text : update.Text,
            RawOutput = update.RawOutput ?? _facts.RawOutput,
            UnifiedDiff = update.UnifiedDiff ?? _facts.UnifiedDiff,
            DiffPath = update.DiffPath ?? _facts.DiffPath,
            DiffOldText = update.DiffOldText ?? _facts.DiffOldText,
            DiffNewText = update.DiffNewText ?? _facts.DiffNewText
        };
        if (update.Status is not null) Status = update.Status;
        Touch(at);
        NotifyTool();
    }

    /// <summary>Records that an event for this item arrived at <paramref name="at"/>.</summary>
    public void Touch(DateTimeOffset? at)
    {
        if (at is not { } time)
            return;
        _firstAt ??= time;
        _lastAt = time;
        if (IsThought)
            this.RaisePropertyChanged(nameof(Label));
    }

    private void NotifyTool()
    {
        foreach (var name in new[]
                 {
                     nameof(Tool), nameof(ToolGlyph), nameof(IsToolRunning), nameof(IsToolFailed), nameof(HasToolSource),
                     nameof(HasToolSummary), nameof(IsToolOpen), nameof(ToolChevron), nameof(HasToolInput), nameof(HasToolOutput),
                     nameof(ToolOutputText), nameof(CanShowAllOutput), nameof(ShowAllOutputLabel), nameof(ToolName),
                     nameof(HasToolDiff), nameof(HasToolDiffOpen), nameof(DiffAdded), nameof(DiffRemoved), nameof(HasDiffAdded),
                     nameof(HasDiffRemoved), nameof(DiffLines), nameof(CanShowWholeDiff), nameof(ShowWholeDiffLabel),
                     nameof(ToolDuration), nameof(HasToolDuration)
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
        ? (_live ? "Thinking" : AgentToolCallProvider.Duration(_firstAt, _lastAt) is { } took ? $"Thought for {took}" : "Thought")
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
