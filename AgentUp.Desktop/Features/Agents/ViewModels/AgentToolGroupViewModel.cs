using System.Reactive;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

/// <summary>
/// Consecutive finished reads, or searches, folded into one row: "Read 3 files". A run that
/// opens ten files in a row reads as one step, and the individual calls are a click away.
/// </summary>
public sealed class AgentToolGroupViewModel : ReactiveObject
{
    private bool _open;

    private AgentToolGroupViewModel(string kind, IReadOnlyList<AgentChatItemViewModel> items)
    {
        Kind = kind;
        Items = items;
        ToggleCommand = ReactiveCommand.Create(() =>
        {
            _open = !_open;
            this.RaisePropertyChanged(nameof(IsOpen));
            this.RaisePropertyChanged(nameof(Chevron));
        });
    }

    public string Kind { get; }
    public IReadOnlyList<AgentChatItemViewModel> Items { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public bool IsOpen => _open;
    public string Chevron => _open ? "⌄" : "›";
    public string AutomationId => DesktopAutomationIds.AgentToolGroup(Items[0].ToolCallId ?? Kind);
    public string Title => Kind == "read" ? $"Read {Items.Count} files" : $"Searched {Items.Count} times";

    /// <summary>What the calls were about, as far as one line holds.</summary>
    public string Summary => string.Join(", ", Items.Select(item => item.Tool.Summary).OfType<string>().Distinct());

    /// <summary>Folds runs of two or more finished calls of the same groupable kind; everything else passes through.</summary>
    public static IReadOnlyList<object> Group(IReadOnlyList<AgentChatItemViewModel> items)
    {
        var entries = new List<object>();
        var index = 0;
        while (index < items.Count)
        {
            var kind = Groupable(items[index]);
            var end = index;
            while (kind is not null && end + 1 < items.Count && Groupable(items[end + 1]) == kind)
                end++;
            if (kind is not null && end > index)
                entries.Add(new AgentToolGroupViewModel(kind, items.Skip(index).Take(end - index + 1).ToArray()));
            else
                entries.Add(items[index]);
            index = end + 1;
        }

        return entries;
    }

    private static string? Groupable(AgentChatItemViewModel item) =>
        item.IsTool && item.Tool.State == "done" && item.ToolKind is "read" or "search" ? item.ToolKind : null;
}
