using System.Collections.ObjectModel;

namespace AgentUp.Desktop.Features.Database.ViewModels;

public sealed class DatabaseRowViewModel
{
    public DatabaseRowViewModel(IReadOnlyList<DatabaseCellViewModel> cells) => Cells = cells;

    public IReadOnlyList<DatabaseCellViewModel> Cells { get; }
}
