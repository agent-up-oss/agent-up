using System.Reactive;
using AgentUp.Desktop.Features.Agents.Providers;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

/// <summary>
/// One stretch of work between two things the agent said. It is drawn open while the agent is
/// working in it and folds into "Worked for 12s · 3 edits, 2 reads" once it is done, unless the
/// reader opened or closed it themselves.
/// </summary>
public sealed class AgentWorkSectionViewModel : ReactiveObject
{
    private bool? _expanded;
    private bool _live;
    private IReadOnlyList<AgentChatItemViewModel> _items = [];
    private IReadOnlyList<object> _entries = [];
    private DateTimeOffset? _startedAt;

    public AgentWorkSectionViewModel(AgentChatItemViewModel first)
    {
        First = first;
        ToggleCommand = ReactiveCommand.Create(() =>
        {
            _expanded = !IsExpanded;
            Notify();
        });
    }

    /// <summary>The section's first entry, which is what identifies it while it grows.</summary>
    public AgentChatItemViewModel First { get; }
    public IReadOnlyList<AgentChatItemViewModel> Items => _items;
    public IReadOnlyList<object> Entries => _entries;
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public string AutomationId => DesktopAutomationIds.AgentRun;
    public bool IsLive => _live;
    public bool IsExpanded => _expanded ?? _live;
    public bool ShowHeader => !_live;
    public string Chevron => IsExpanded ? "⌄" : "›";

    public string Summary => AgentEventPresentationProvider.WorkSummary(
        _items.Select(item => item.WorkKind),
        AgentToolCallProvider.Duration(_startedAt ?? _items.FirstOrDefault()?.StartedAt, _items.Select(item => item.EndedAt).Max()),
        _items.Count(item => item.IsToolFailed));

    /// <summary>
    /// Takes the section's current entries and whether it is the one being worked in. Lists are
    /// replaced only when what they hold changed, so streamed text does not redraw the section.
    /// </summary>
    public void Update(IReadOnlyList<AgentChatItemViewModel> items, bool live, DateTimeOffset? startedAt)
    {
        var changedItems = !_items.SequenceEqual(items);
        _items = items;
        _startedAt = startedAt;
        var entries = AgentWorkFoldViewModel.Fold(items, _entries);
        var changedEntries = !_entries.SequenceEqual(entries);
        if (changedEntries)
            _entries = entries;
        var changedLive = _live != live;
        _live = live;
        if (changedItems) this.RaisePropertyChanged(nameof(Items));
        if (changedEntries) this.RaisePropertyChanged(nameof(Entries));
        if (changedLive) Notify();
        this.RaisePropertyChanged(nameof(Summary));
    }

    private void Notify()
    {
        this.RaisePropertyChanged(nameof(IsLive));
        this.RaisePropertyChanged(nameof(IsExpanded));
        this.RaisePropertyChanged(nameof(ShowHeader));
        this.RaisePropertyChanged(nameof(Chevron));
    }
}
