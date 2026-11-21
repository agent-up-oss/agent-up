using System.Reactive;
using AgentUp.Desktop.Features.Agents.Providers;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

/// <summary>
/// Consecutive finished edits, reads, searches and thoughts folded into one line: "25 edits,
/// 10 reads, 3 thoughts". A section that touches thirty files reads as one step, and the
/// individual entries are a click away.
/// </summary>
public sealed class AgentWorkFoldViewModel : ReactiveObject
{
    private bool _open;

    private AgentWorkFoldViewModel(IReadOnlyList<AgentChatItemViewModel> items)
    {
        Items = items;
        ToggleCommand = ReactiveCommand.Create(() =>
        {
            _open = !_open;
            this.RaisePropertyChanged(nameof(IsOpen));
            this.RaisePropertyChanged(nameof(Chevron));
        });
    }

    public IReadOnlyList<AgentChatItemViewModel> Items { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public bool IsOpen => _open;
    public string Chevron => _open ? "⌄" : "›";
    public string AutomationId => DesktopAutomationIds.AgentToolGroup(Items[0].ToolCallId ?? Items[0].WorkKind);
    public string Title => AgentEventPresentationProvider.WorkCounts(Items.Select(item => item.WorkKind));

    /// <summary>What the calls were about, as far as one line holds.</summary>
    public string Summary => string.Join(", ", Items.Where(item => item.IsTool).Select(item => item.Tool.Summary).OfType<string>().Distinct());

    /// <summary>
    /// The entries of a work section: runs of two or more foldable steps become one fold, and
    /// everything else, the live entry included, stays its own line. A fold already drawn is kept,
    /// with whether the reader opened it, while its run is unchanged.
    /// </summary>
    public static IReadOnlyList<object> Fold(
        IReadOnlyList<AgentChatItemViewModel> items,
        IReadOnlyList<object>? previous = null)
    {
        var kept = (previous ?? []).OfType<AgentWorkFoldViewModel>().ToArray();
        var entries = new List<object>();
        var index = 0;
        while (index < items.Count)
        {
            var end = index;
            while (Foldable(items[index]) && end + 1 < items.Count && Foldable(items[end + 1]))
                end++;
            if (end > index)
            {
                var run = items.Skip(index).Take(end - index + 1).ToArray();
                entries.Add(kept.FirstOrDefault(fold => fold.Items.SequenceEqual(run)) ?? new AgentWorkFoldViewModel(run));
            }
            else
            {
                entries.Add(items[index]);
            }

            index = end + 1;
        }

        return entries;
    }

    private static bool Foldable(AgentChatItemViewModel item) =>
        !item.IsLive
        && item.WorkKind switch
        {
            "thought" => true,
            "edit" or "read" or "search" => item.Tool.State == "done",
            _ => false
        };
}
