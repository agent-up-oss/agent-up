using AgentUp.Desktop.Features.Console.Http;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Http;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using Avalonia.Controls;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class AppDriver
{
    private readonly MainWindow _window;

    public SidebarDriver Sidebar { get; }
    public ContentDriver Content { get; }

    internal MainWindow Window => _window;

    private AppDriver(MainWindow window)
    {
        _window = window;
        Sidebar = new SidebarDriver(window);
        Content = new ContentDriver(window);
    }

    public static async Task<AppDriver> LaunchEmptyAsync()
        => await LaunchAsync([]);

    public static async Task<AppDriver> LaunchEmptyAsync(FirstRunTutorialViewModel tutorial)
        => await LaunchAsync([], tutorial: tutorial);

    public static async Task<AppDriver> LaunchWithWorkspaceAsync(WorkspaceDto workspace)
        => await LaunchAsync([workspace]);

    public static async Task<AppDriver> LaunchWithWorkspaceAsync(
        WorkspaceDto workspace,
        Func<NativeWebView> webViewFactory)
        => await LaunchAsync([workspace], webViewFactory);

    public static async Task<AppDriver> LaunchWithWorkspacesAsync(List<WorkspaceDto> workspaces)
        => await LaunchAsync(workspaces);

    public static async Task<AppDriver> LaunchWithWorkspacesAsync(
        List<WorkspaceDto> workspaces,
        Func<NativeWebView> webViewFactory)
        => await LaunchAsync(workspaces, webViewFactory);

    public static async Task<AppDriver> LaunchWithServerErrorAsync()
    {
        var handler = new ErrorHttpMessageHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var workspaceClient = new WorkspaceApiClient(http);
        var consoleClient = new ConsoleApiClient(http);
        return await LaunchWithClientsAsync(workspaceClient, consoleClient);
    }

    public static async Task<(AppDriver Driver, MutableFakeHttpMessageHandler Handler)> LaunchWithMutableWorkspacesAsync(List<WorkspaceDto> initial)
    {
        var handler = new MutableFakeHttpMessageHandler(initial);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var workspaceClient = new WorkspaceApiClient(http);
        var consoleClient = new ConsoleApiClient(http);
        var driver = await LaunchWithClientsAsync(workspaceClient, consoleClient);
        return (driver, handler);
    }

    public static async Task<AppDriver> LaunchWithWorkspacesAndOutputAsync(
        List<WorkspaceDto> workspaces,
        Dictionary<string, List<string>> outputLines)
    {
        var handler = new FakeHttpMessageHandler(workspaces, outputLines);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var workspaceClient = new WorkspaceApiClient(http);
        var consoleClient = new ConsoleApiClient(http);
        return await LaunchWithClientsAsync(workspaceClient, consoleClient);
    }

    private static async Task<AppDriver> LaunchAsync(
        List<WorkspaceDto> workspaces,
        Func<NativeWebView>? webViewFactory = null,
        FirstRunTutorialViewModel? tutorial = null)
    {
        var handler = new FakeHttpMessageHandler(workspaces);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var workspaceClient = new WorkspaceApiClient(http);
        var consoleClient = new ConsoleApiClient(http);
        return await LaunchWithClientsAsync(workspaceClient, consoleClient, webViewFactory, tutorial);
    }

    private static async Task<AppDriver> LaunchWithClientsAsync(
        WorkspaceApiClient workspaceClient,
        ConsoleApiClient consoleClient,
        Func<NativeWebView>? webViewFactory = null,
        FirstRunTutorialViewModel? tutorial = null)
    {
        var vm = new MainViewModel(workspaceClient, consoleClient, tutorial ?? CompletedTutorial());
        var window = new MainWindow { DataContext = vm };
        if (webViewFactory is not null)
            window.WebViewFactory = webViewFactory;
        window.Show();

        await vm.InitializeAsync();
        await HeadlessExtensions.FlushAsync();

        return new AppDriver(window);
    }

    private static FirstRunTutorialViewModel CompletedTutorial()
        => new(
            new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(true, false, 7)),
            new PassingTutorialChecks());

    private sealed class InMemoryTutorialSettingsStore(FirstRunTutorialSettings settings) : IFirstRunTutorialSettingsStore
    {
        public Task<FirstRunTutorialSettings> LoadAsync() => Task.FromResult(settings);

        public Task SaveAsync(FirstRunTutorialSettings settings) => Task.CompletedTask;
    }

    private sealed class PassingTutorialChecks : IFirstRunTutorialChecks
    {
        public Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Docker works."));

        public Task<FirstRunCheckResult> CheckNodeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Node works."));

        public Task<FirstRunCheckResult> CreateJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Sample created."));

        public Task<FirstRunCheckResult> CheckJavaScriptProjectFilesAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Project files work."));

        public Task<FirstRunCheckResult> CreateAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("agent-up.json created."));

        public Task<FirstRunCheckResult> CheckAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("agent-up.json works."));

        public Task<FirstRunCheckResult> StartJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Started."));

        public Task<FirstRunCheckResult> CheckJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Workspace works."));

        public Task<FirstRunCheckResult> CheckDuplicatedJavaScriptWorkspacesAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Duplicate works."));
    }
}
