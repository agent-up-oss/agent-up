using System.Collections.ObjectModel;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Database.ViewModels;

public sealed class DatabaseRowViewModel : ReactiveObject
{
    public DatabaseRowViewModel(IReadOnlyList<string> cells) => Cells = cells;

    public IReadOnlyList<string> Cells { get; }
}
