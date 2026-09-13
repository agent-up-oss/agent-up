using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using AgentUp.Desktop.Features.Agents.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

public sealed class AgentRunViewModel : ReactiveObject
{
    private bool _live = true;
    private bool? _expanded;

    public AgentRunViewModel()
    {
        Items.CollectionChanged += OnItemsChanged;
        ToggleCommand = ReactiveCommand.Create(Toggle);
    }

    public ObservableCollection<AgentChatItemViewModel> Items { get; } = [];
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public bool IsLive => _live;
    public bool IsExpanded => _expanded ?? _live;
    public bool HasWork => WorkItems.Count > 0;
    public bool HasReply => Reply is not null;
    public bool ShowHeader => !_live && HasWork;
    public string Chevron => IsExpanded ? "▾" : "▸";
    public IReadOnlyList<AgentChatItemViewModel> WorkItems => Split().Work;
    public AgentChatItemViewModel? Reply => Split().Reply;
    public string Summary => AgentEventPresentationProvider.RunSummary(WorkItems.Select(item => item.Role));

    public void Seal()
    {
        if (!_live)
            return;
        _live = false;
        _expanded = false;
        NotifyRun();
    }

    private void Toggle()
    {
        _expanded = !IsExpanded;
        NotifyRun();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
        => NotifyRun();

    private (IReadOnlyList<AgentChatItemViewModel> Work, AgentChatItemViewModel? Reply) Split()
    {
        var lastAgent = -1;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i].Role != "Agent")
                continue;
            lastAgent = i;
            break;
        }

        if (lastAgent < 0)
            return (Items.ToList(), null);

        var work = new List<AgentChatItemViewModel>(Items.Count - 1);
        for (var i = 0; i < Items.Count; i++)
        {
            if (i != lastAgent)
                work.Add(Items[i]);
        }

        return (work, Items[lastAgent]);
    }

    private void NotifyRun()
    {
        this.RaisePropertyChanged(nameof(IsLive));
        this.RaisePropertyChanged(nameof(IsExpanded));
        this.RaisePropertyChanged(nameof(HasWork));
        this.RaisePropertyChanged(nameof(HasReply));
        this.RaisePropertyChanged(nameof(ShowHeader));
        this.RaisePropertyChanged(nameof(Chevron));
        this.RaisePropertyChanged(nameof(WorkItems));
        this.RaisePropertyChanged(nameof(Reply));
        this.RaisePropertyChanged(nameof(Summary));
    }
}
