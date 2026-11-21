using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

/// <summary>
/// One turn of the agent. What it says - summaries midway and its final reply - is always shown;
/// the work between them is drawn as sections that fold once the agent has moved past them.
/// </summary>
public sealed class AgentRunViewModel : ReactiveObject
{
    private bool _live = true;
    private DateTimeOffset? _startedAt;
    private IReadOnlyList<object> _segments = [];
    private readonly Dictionary<AgentChatItemViewModel, AgentWorkSectionViewModel> _sections = [];

    public AgentRunViewModel()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    public ObservableCollection<AgentChatItemViewModel> Items { get; } = [];
    public string AutomationId { get; } = DesktopAutomationIds.AgentRun;
    public bool IsLive => _live;

    /// <summary>
    /// The turn as drawn, in order: <see cref="AgentWorkSectionViewModel"/> for each stretch of
    /// work, and the <see cref="AgentChatItemViewModel"/> of each thing the agent said.
    /// </summary>
    public IReadOnlyList<object> Segments => _segments;
    public IEnumerable<AgentWorkSectionViewModel> Sections => _segments.OfType<AgentWorkSectionViewModel>();

    /// <summary>Every step that is not something the agent said.</summary>
    public IReadOnlyList<AgentChatItemViewModel> WorkItems => Items.Where(item => !item.IsAgentText).ToArray();

    /// <summary>The agent's closing words: what it said last, when nothing came after.</summary>
    public AgentChatItemViewModel? Reply => _segments.LastOrDefault() as AgentChatItemViewModel;
    public bool HasReply => Reply is not null;

    /// <summary>Records that an event for this turn arrived at <paramref name="at"/>; the first one starts its clock.</summary>
    public void Touch(DateTimeOffset? at)
    {
        if (at is { } time)
            _startedAt ??= time;
    }

    /// <summary>The turn ended: every section folds.</summary>
    public void Seal()
    {
        if (!_live)
            return;
        _live = false;
        Refresh();
    }

    /// <summary>Re-reads which section is live and what each one folds, after a step changed state.</summary>
    public void Refresh()
    {
        var segments = new List<object>();
        var work = new List<AgentChatItemViewModel>();
        var sectionIndex = 0;
        void Close(bool last)
        {
            if (work.Count == 0)
                return;
            if (!_sections.TryGetValue(work[0], out var section))
                _sections[work[0]] = section = new AgentWorkSectionViewModel(work[0]);
            // The first section's clock starts at the prompt, so thinking before the first step counts.
            section.Update(work.ToArray(), _live && last, sectionIndex++ == 0 ? _startedAt : null);
            segments.Add(section);
            work.Clear();
        }

        foreach (var item in Items)
        {
            if (!item.IsAgentText)
            {
                work.Add(item);
                continue;
            }

            Close(last: false);
            segments.Add(item);
        }

        Close(last: true);
        foreach (var said in segments.OfType<AgentChatItemViewModel>())
            said.IsClosing = ReferenceEquals(said, segments[^1]);
        if (_segments.SequenceEqual(segments))
            return;
        _segments = segments;
        this.RaisePropertyChanged(nameof(Segments));
        this.RaisePropertyChanged(nameof(Reply));
        this.RaisePropertyChanged(nameof(HasReply));
        this.RaisePropertyChanged(nameof(IsLive));
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs args) => Refresh();
}
