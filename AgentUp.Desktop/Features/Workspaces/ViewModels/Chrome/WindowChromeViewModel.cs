using System.Collections.ObjectModel;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels.Chrome;

public sealed class WindowChromeViewModel
{
    public ObservableCollection<object> LeftItems { get; } = [];

    public void SetLeftItems(IEnumerable<object> items)
    {
        LeftItems.Clear();
        foreach (var item in items)
            LeftItems.Add(item);
    }
}
