using AgentUp.Desktop.Features.Workspaces.Http;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class AppDriver
{
    private readonly MainWindow _window;

    public SidebarDriver Sidebar { get; }
    public ContentDriver Content { get; }

    private AppDriver(MainWindow window)
    {
        _window = window;
        Sidebar = new SidebarDriver(window);
        Content = new ContentDriver(window);
    }

    public static async Task<AppDriver> LaunchEmptyAsync()
        => await LaunchAsync([]);

    public static async Task<AppDriver> LaunchWithWorkspaceAsync(WorkspaceDto workspace)
        => await LaunchAsync([workspace]);

    public static async Task<AppDriver> LaunchWithWorkspacesAsync(List<WorkspaceDto> workspaces)
        => await LaunchAsync(workspaces);

    public static async Task<AppDriver> LaunchWithServerErrorAsync()
    {
        var handler = new ErrorHttpMessageHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var apiClient = new WorkspaceApiClient(http);
        return await LaunchWithClientAsync(apiClient);
    }

    public static async Task<(AppDriver Driver, MutableFakeHttpMessageHandler Handler)> LaunchWithMutableWorkspacesAsync(List<WorkspaceDto> initial)
    {
        var handler = new MutableFakeHttpMessageHandler(initial);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var apiClient = new WorkspaceApiClient(http);
        var driver = await LaunchWithClientAsync(apiClient);
        return (driver, handler);
    }

    public static async Task<AppDriver> LaunchWithWorkspacesAndOutputAsync(
        List<WorkspaceDto> workspaces,
        Dictionary<string, List<string>> outputLines)
    {
        var handler = new FakeHttpMessageHandler(workspaces, outputLines);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var apiClient = new WorkspaceApiClient(http);
        return await LaunchWithClientAsync(apiClient);
    }

    private static async Task<AppDriver> LaunchAsync(List<WorkspaceDto> workspaces)
    {
        var handler = new FakeHttpMessageHandler(workspaces);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var apiClient = new WorkspaceApiClient(http);
        return await LaunchWithClientAsync(apiClient);
    }

    private static async Task<AppDriver> LaunchWithClientAsync(WorkspaceApiClient apiClient)
    {
        var vm = new MainViewModel(apiClient);
        var window = new MainWindow { DataContext = vm };
        window.Show();

        await vm.InitializeAsync();
        await HeadlessExtensions.FlushAsync();

        return new AppDriver(window);
    }
}
