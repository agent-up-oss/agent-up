using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Console.Http;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Http;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    public WorkspaceListViewModel Sidebar { get; }
    public ApplicationListViewModel Applications { get; }
    public ConsoleViewModel Console { get; }

    public MainViewModel(WorkspaceApiClient workspaceClient, ConsoleApiClient consoleClient)
    {
        Sidebar = new WorkspaceListViewModel(workspaceClient);
        Applications = new ApplicationListViewModel();
        Console = new ConsoleViewModel(consoleClient);

        Sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .Subscribe(ws =>
            {
                Console.Clear();
                Applications.Update(ws?.Applications ?? []);
            });

        Applications.WhenAnyValue(x => x.SelectedApplication)
            .Subscribe(app =>
            {
                var workspaceId = Sidebar.SelectedWorkspace?.Id;
                if (app is null || workspaceId is null) return;
                _ = Console.LoadAsync(workspaceId, app.Name);
            });
    }

    public async Task InitializeAsync() => await Sidebar.LoadAsync();
}
