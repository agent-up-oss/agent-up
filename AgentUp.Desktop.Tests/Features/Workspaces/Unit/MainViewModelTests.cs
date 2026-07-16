using System.Reactive.Linq;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public class MainViewModelTests
{
    [Test]
    public void SidebarWidth_is220WhenExpanded()
    {
        var vm = new MainViewModel(NullWorkspaceClient(), NullConsoleClient());
        Assert.That(vm.Sidebar.Width, Is.EqualTo(220));
    }

    [Test]
    public void SidebarWidth_is56WhenCollapsed()
    {
        var vm = new MainViewModel(NullWorkspaceClient(), NullConsoleClient());
        vm.Sidebar.IsCollapsed = true;
        Assert.That(vm.Sidebar.Width, Is.EqualTo(56));
    }

    [Test]
    public void IsSidebarExpanded_invertsIsSidebarCollapsed()
    {
        var vm = new MainViewModel(NullWorkspaceClient(), NullConsoleClient());

        Assert.That(vm.Sidebar.IsExpanded, Is.True);
        vm.Sidebar.IsCollapsed = true;
        Assert.That(vm.Sidebar.IsExpanded, Is.False);
        vm.Sidebar.IsCollapsed = false;
        Assert.That(vm.Sidebar.IsExpanded, Is.True);
    }

    [Test]
    public void SidebarToggleIcon_changesWithCollapsedState()
    {
        var vm = new MainViewModel(NullWorkspaceClient(), NullConsoleClient());
        Assert.That(vm.Sidebar.ToggleIcon, Is.EqualTo("‹"));
        vm.Sidebar.IsCollapsed = true;
        Assert.That(vm.Sidebar.ToggleIcon, Is.EqualTo("›"));
    }

    [Test]
    public async Task InitializeAsync_setsErrorMessage_whenServerUnreachable()
    {
        var vm = new MainViewModel(NullWorkspaceClient(), NullConsoleClient());
        await vm.InitializeAsync();

        Assert.That(vm.Sidebar.ErrorMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task InitializeAsync_populatesWorkspaces_onSuccess()
    {
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "feat/x", "abc123", "Stopped");
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();

        Assert.That(vm.Sidebar.Workspaces, Has.Count.EqualTo(1));
        Assert.That(vm.Sidebar.Workspaces[0].DisplayName, Is.EqualTo("My App"));
        Assert.That(vm.Sidebar.ErrorMessage, Is.Null);
        Assert.That(vm.Sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task InitializeAsync_selectsFirstWorkspace_automaticallyOnFirstLoad()
    {
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "feat/x", "abc123", "Stopped");
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();

        Assert.That(vm.Sidebar.SelectedWorkspace, Is.Not.Null);
        Assert.That(vm.Sidebar.SelectedWorkspace!.Id, Is.EqualTo("ws-1"));
    }

    [Test]
    public async Task InitializeAsync_selectsFirstConfiguredPortSubTab_whenApplicationHasPorts()
    {
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "main", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("App", "cmd", null, "Running")
                {
                    AllocatedPorts =
                    [
                        new PortMappingDto("WEB_PORT", 3000, 5100),
                        new PortMappingDto("API_PORT", 5000, 5101)
                    ]
                }
            ]
        };
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();

        Assert.That(vm.SubTabs.Select(tab => tab.Label), Is.EqualTo(["3000:5100", "5000:5101", "Console"]));
        Assert.That(vm.SelectedSubTab, Is.TypeOf<PortSubTabViewModel>());
        Assert.That(((PortSubTabViewModel)vm.SelectedSubTab!).AllocatedPort, Is.EqualTo(5100));
        Assert.That(vm.ShowPortView, Is.True);
    }

    [Test]
    public async Task InitializeAsync_setsAddressBarToFirstHttpPortUrl_whenApplicationHasPorts()
    {
        const int port = 5100;
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "main", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("App", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto("WEB_PORT", 3000, port)]
                }
            ]
        };
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();

        Assert.That(vm.AddressBarUrl, Is.EqualTo($"http://localhost:{port}/"));
    }

    [Test]
    public async Task NavigateAddressCommand_emitsEditedAddress_whenHttpPortTabSelected()
    {
        var dto = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();
        vm.AddressBarUrl = "http://localhost:3000/settings";
        vm.NavigateAddressCommand.Execute().Subscribe();

        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == "http://localhost:3000/settings"));
    }

    [Test]
    public async Task NavigateAddressCommand_prefixesHttpScheme_whenEditedAddressHasNoScheme()
    {
        var dto = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();
        vm.AddressBarUrl = "localhost:3000/settings";
        vm.NavigateAddressCommand.Execute().Subscribe();

        Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:3000/settings"));
        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == "http://localhost:3000/settings"));
    }

    [Test]
    public async Task UpdateAddressFromBrowser_updatesAddressBar_whenSelectedHttpPortNavigates()
    {
        var dto = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();
        vm.UpdateAddressFromBrowser("ws-1", "http://localhost:3000/dashboard");

        Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:3000/dashboard"));
    }

    [Test]
    public void BrowserCommands_emitRequestedBrowserActions()
    {
        var vm = new MainViewModel(NullWorkspaceClient(), NullConsoleClient());
        var commands = new List<BrowserCommand>();
        vm.BrowserCommands.Subscribe(commands.Add);

        vm.BrowserBackCommand.Execute().Subscribe();
        vm.BrowserForwardCommand.Execute().Subscribe();
        vm.BrowserReloadCommand.Execute().Subscribe();

        Assert.That(commands, Is.EqualTo([BrowserCommand.Back, BrowserCommand.Forward, BrowserCommand.Reload]));
    }

    [Test]
    public async Task SidebarReload_reselectsRefreshedWorkspaceAndEmitsSelectedPortNavigation()
    {
        var initial = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var refreshed = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var handler = new MutableFakeHttpMessageHandler([initial]);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = new MainViewModel(new WorkspaceApiClient(http), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();
        var previousSelected = vm.Sidebar.SelectedWorkspace;
        emissions.Clear();
        handler.SetWorkspaces([refreshed]);
        await vm.Sidebar.LoadAsync();

        Assert.That(vm.Sidebar.SelectedWorkspace, Is.Not.SameAs(previousSelected));
        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == "http://localhost:3000/"));
    }

    [Test]
    public async Task TutorialStepTransition_reloadsWorkspaceListBehindOverlay()
    {
        var initial = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var handler = new MutableFakeHttpMessageHandler([initial]);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var tutorial = new FirstRunTutorialViewModel(
            new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(false, false, 0)),
            new PassingTutorialChecks());
        var vm = new MainViewModel(new WorkspaceApiClient(http), NullConsoleClient(), tutorial);
        var browserCommands = new List<BrowserCommand>();
        vm.BrowserCommands.Subscribe(browserCommands.Add);

        await vm.InitializeAsync();
        var requestCountAfterInitialize = handler.RequestCount;

        await tutorial.CheckDockerCommand.Execute().FirstAsync();
        await tutorial.ContinueCommand.Execute().FirstAsync();
        await Task.Delay(25);

        Assert.That(handler.RequestCount, Is.GreaterThan(requestCountAfterInitialize));
        Assert.That(browserCommands, Does.Contain(BrowserCommand.Reload));
    }

    [Test]
    public async Task InitializeAsync_selectsConsoleSubTab_whenApplicationHasNoPorts()
    {
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "main", "abc123", "Running")
        {
            Applications = [new ApplicationDto("Worker", "cmd", null, "Running")]
        };
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();

        Assert.That(vm.SubTabs.Select(tab => tab.Label), Is.EqualTo(["Console"]));
        Assert.That(vm.SelectedSubTab, Is.TypeOf<ConsoleSubTabViewModel>());
        Assert.That(vm.ShowConsole, Is.True);
        Assert.That(vm.AddressBarUrl, Is.Null);
    }

    [Test]
    public async Task BrowserNavigation_emitsPortUrl_whenPortSubTabSelected()
    {
        const int port = 3000;
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "main", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("App", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, port, port)]
                }
            ]
        };
        var vm = new MainViewModel(FakeWorkspaceClient([dto]), NullConsoleClient());

        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();

        // Auto-selects the first app and its first port; selecting it again keeps this assertion explicit.
        var portTab = vm.SubTabs.OfType<PortSubTabViewModel>().First();
        vm.SelectedSubTab = portTab;

        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == $"http://localhost:{port}/"),
            "Selecting the port sub-tab must emit the workspace id and the port's HTTP URL");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static WorkspaceApiClient NullWorkspaceClient()
    {
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:0") };
        return new WorkspaceApiClient(http);
    }

    private static ConsoleApiClient NullConsoleClient()
    {
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:0") };
        return new ConsoleApiClient(http);
    }

    private static WorkspaceApiClient FakeWorkspaceClient(List<WorkspaceDto> workspaces)
    {
        var handler = new FakeHttpMessageHandler(workspaces);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        return new WorkspaceApiClient(http);
    }

    private sealed class InMemoryTutorialSettingsStore(FirstRunTutorialSettings settings) : IFirstRunTutorialSettingsStore
    {
        public Task<FirstRunTutorialSettings> LoadAsync() => Task.FromResult(settings);

        public Task SaveAsync(FirstRunTutorialSettings settings) => Task.CompletedTask;
    }

    private sealed class PassingTutorialChecks : IFirstRunTutorialChecks
    {
        public Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Docker works."));

        public Task<FirstRunCheckResult> CheckNodeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Node works."));

        public Task<FirstRunSampleProjectResult> CreateJavaScriptSampleAsync(string? currentProjectDirectory = null, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunSampleProjectResult.Success("Sample created.", currentProjectDirectory ?? "/tmp/tutorial/agent-up-tutorial/example-agent1"));

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

        public Task<FirstRunCheckResult> CreateDuplicatedJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Duplicate created."));

        public Task<FirstRunCheckResult> CheckDuplicatedJavaScriptWorkspacesAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Duplicate works."));
    }
}
