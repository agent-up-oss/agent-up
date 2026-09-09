using System.Reactive.Linq;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels.Chrome;

public sealed class ChromeServerStatusViewModel : ReactiveObject
{
    private readonly WorkspaceListViewModel _sidebar;

    public ChromeServerStatusViewModel(WorkspaceListViewModel sidebar)
    {
        _sidebar = sidebar;
        _sidebar.WhenAnyValue(viewModel => viewModel.ServerStatusText)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ServerStatusText)));
        _sidebar.WhenAnyValue(viewModel => viewModel.ServerStatusColor)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ServerStatusColor)));
    }

    public string ServerStatusText => _sidebar.ServerStatusText;

    public string ServerStatusColor => _sidebar.ServerStatusColor;
}
