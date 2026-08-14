using System.Reactive.Linq;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.FirstRun.Interfaces;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public class MainViewModelTests
{
    [Test]
    public void SidebarWidth_is220WhenExpanded()
    {
        var vm = MainViewModelFactory.Create(NullWorkspaceClient(), NullConsoleClient());
        Assert.That(vm.Sidebar.Width, Is.EqualTo(220));
    }

    [Test]
    public void SidebarWidth_is56WhenCollapsed()
    {
        var vm = MainViewModelFactory.Create(NullWorkspaceClient(), NullConsoleClient());
        vm.Sidebar.IsCollapsed = true;
        Assert.That(vm.Sidebar.Width, Is.EqualTo(56));
    }

    [Test]
    public void IsSidebarExpanded_invertsIsSidebarCollapsed()
    {
        var vm = MainViewModelFactory.Create(NullWorkspaceClient(), NullConsoleClient());

        Assert.That(vm.Sidebar.IsExpanded, Is.True);
        vm.Sidebar.IsCollapsed = true;
        Assert.That(vm.Sidebar.IsExpanded, Is.False);
        vm.Sidebar.IsCollapsed = false;
        Assert.That(vm.Sidebar.IsExpanded, Is.True);
    }

    [Test]
    public void SidebarToggleIcon_changesWithCollapsedState()
    {
        var vm = MainViewModelFactory.Create(NullWorkspaceClient(), NullConsoleClient());
        Assert.That(vm.Sidebar.ToggleIcon, Is.EqualTo("‹"));
        vm.Sidebar.IsCollapsed = true;
        Assert.That(vm.Sidebar.ToggleIcon, Is.EqualTo("›"));
    }

    [Test]
    public async Task InitializeAsync_setsErrorMessage_whenServerUnreachable()
    {
        var vm = MainViewModelFactory.Create(NullWorkspaceClient(), NullConsoleClient());
        await vm.InitializeAsync();

        Assert.That(vm.Sidebar.ErrorMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task InitializeAsync_populatesWorkspaces_onSuccess()
    {
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "feat/x", "abc123", "Stopped");
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());

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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());

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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());

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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();

        Assert.That(vm.AddressBarUrl, Is.EqualTo($"http://localhost:{port}/"));
    }

    [Test]
    public async Task NavigateAddressCommand_emitsEditedAddress_whenHttpPortTabSelected()
    {
        var dto = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());
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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());
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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();
        vm.UpdateAddressFromBrowser("ws-1", "http://localhost:3000/dashboard");

        Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:3000/dashboard"));
    }

    [Test]
    public void BrowserCommands_emitRequestedBrowserActions()
    {
        var vm = MainViewModelFactory.Create(NullWorkspaceClient(), NullConsoleClient());
        var commands = new List<BrowserCommand>();
        vm.BrowserCommands.Subscribe(commands.Add);

        vm.BrowserBackCommand.Execute().Subscribe();
        vm.BrowserForwardCommand.Execute().Subscribe();
        vm.BrowserReloadCommand.Execute().Subscribe();

        Assert.That(commands, Is.EqualTo([BrowserCommand.Back, BrowserCommand.Forward, BrowserCommand.Reload]));
    }

    [Test]
    public async Task SidebarReload_preservesWorkspaceReferenceAndEmitsNoBrowserNavigation()
    {
        // LoadAsync merges workspace state in-place so SelectedWorkspace keeps the same
        // reference. This prevents the reactive chain from firing and resetting active
        // browser sessions mid-reload.
        var initial = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var refreshed = WorkspaceFixtures.WithHttpPort("ws-1", 3000) with { State = "Running" };
        var handler = new MutableFakeHttpMessageHandler([initial]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = MainViewModelFactory.Create(new WorkspaceApiClient(http), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();
        var previousSelected = vm.Sidebar.SelectedWorkspace;
        emissions.Clear();
        handler.SetWorkspaces([refreshed]);
        await vm.Sidebar.LoadAsync();

        Assert.That(vm.Sidebar.SelectedWorkspace, Is.SameAs(previousSelected));
        Assert.That(previousSelected!.State, Is.EqualTo("Running"));
        Assert.That(emissions, Is.Empty);
    }

    [Test]
    public async Task SidebarReload_rebuildsSelectedPortTabAndNavigates_whenAllocatedPortChanges()
    {
        var initial = WorkspaceFixtures.WithHttpPort("ws-1", 10000);
        var refreshed = WorkspaceFixtures.WithHttpPort("ws-1", 10200) with { State = "Running" };
        var handler = new MutableFakeHttpMessageHandler([initial]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = MainViewModelFactory.Create(new WorkspaceApiClient(http), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();
        emissions.Clear();

        handler.SetWorkspaces([refreshed]);
        await vm.Sidebar.LoadAsync();

        Assert.That(((PortSubTabViewModel)vm.SelectedSubTab!).AllocatedPort, Is.EqualTo(10200));
        Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:10200/"));
        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == "http://localhost:10200/"));
    }

    [Test]
    public async Task ScopedWorkspaceRefresh_fetchesOnlyChangedWorkspaceAndNavigates_whenSelectedPortChanges()
    {
        var initialWs1 = WorkspaceFixtures.WithHttpPort("ws-1", 10200);
        var initialWs2 = WorkspaceFixtures.WithHttpPort("ws-2", 20200);
        var refreshedWs1 = WorkspaceFixtures.WithHttpPort("ws-1", 10300) with { State = "Running" };
        var handler = new MutableFakeHttpMessageHandler([initialWs1, initialWs2]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = MainViewModelFactory.Create(new WorkspaceApiClient(http), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();
        emissions.Clear();
        handler.RequestPaths.Clear();

        handler.SetWorkspaces([refreshedWs1, initialWs2]);
        await vm.Sidebar.RefreshWorkspaceAsync("ws-1");

        Assert.That(handler.RequestPaths, Is.EqualTo(["/api/workspaces/ws-1"]));
        Assert.That(((PortSubTabViewModel)vm.SelectedSubTab!).AllocatedPort, Is.EqualTo(10300));
        Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:10300/"));
        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == "http://localhost:10300/"));
    }

    [Test]
    public async Task ScopedWorkspaceRefresh_doesNotNavigateActiveBrowser_whenNonSelectedWorkspacePortChanges()
    {
        var initialWs1 = WorkspaceFixtures.WithHttpPort("ws-1", 10200);
        var initialWs2 = WorkspaceFixtures.WithHttpPort("ws-2", 20200);
        var refreshedWs2 = WorkspaceFixtures.WithHttpPort("ws-2", 20300) with { State = "Running" };
        var handler = new MutableFakeHttpMessageHandler([initialWs1, initialWs2]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = MainViewModelFactory.Create(new WorkspaceApiClient(http), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();
        var selectedWorkspace = vm.Sidebar.SelectedWorkspace;
        emissions.Clear();
        handler.RequestPaths.Clear();

        handler.SetWorkspaces([initialWs1, refreshedWs2]);
        await vm.Sidebar.RefreshWorkspaceAsync("ws-2");

        Assert.That(handler.RequestPaths, Is.EqualTo(["/api/workspaces/ws-2"]));
        Assert.That(vm.Sidebar.SelectedWorkspace, Is.SameAs(selectedWorkspace));
        Assert.That(((PortSubTabViewModel)vm.SelectedSubTab!).AllocatedPort, Is.EqualTo(10200));
        Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:10200/"));
        Assert.That(emissions, Is.Empty);
    }

    [Test]
    public async Task ScopedWorkspaceRefresh_clearsPreviousErrorMessage_whenRefreshSucceeds()
    {
        var initial = WorkspaceFixtures.WithHttpPort("ws-1", 10200);
        var refreshed = WorkspaceFixtures.WithHttpPort("ws-1", 10300);
        var handler = new MutableFakeHttpMessageHandler([initial]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = MainViewModelFactory.Create(new WorkspaceApiClient(http), NullConsoleClient());

        await vm.InitializeAsync();
        vm.Sidebar.ErrorMessage = "Could not refresh workspace 'ws-1': previous failure";

        handler.SetWorkspaces([refreshed]);
        await vm.Sidebar.RefreshWorkspaceAsync("ws-1");

        Assert.That(vm.Sidebar.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task ScopedWorkspaceRefresh_doesNotSetErrorMessage_whenCallerCancels()
    {
        var initial = WorkspaceFixtures.WithHttpPort("ws-1", 10200);
        var handler = new MutableFakeHttpMessageHandler([initial]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = MainViewModelFactory.Create(new WorkspaceApiClient(http), NullConsoleClient());
        using var cts = new CancellationTokenSource();

        await vm.InitializeAsync();
        await cts.CancelAsync();
        await vm.Sidebar.RefreshWorkspaceAsync("ws-1", cts.Token);

        Assert.That(vm.Sidebar.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task TutorialStepTransition_reloadsWorkspaceListBehindOverlay()
    {
        var initial = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var handler = new MutableFakeHttpMessageHandler([initial]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var tutorial = new FirstRunTutorialViewModel(
            new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(false, false, 0)),
            new PassingTutorialChecks());
        var vm = MainViewModelFactory.Create(new WorkspaceApiClient(http), NullConsoleClient(), tutorial: tutorial);
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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();

        Assert.That(vm.SubTabs.Select(tab => tab.Label), Is.EqualTo(["Console"]));
        Assert.That(vm.SelectedSubTab, Is.TypeOf<ConsoleSubTabViewModel>());
        Assert.That(vm.ShowConsole, Is.True);
        Assert.That(vm.AddressBarUrl, Is.Null);
    }

    [Test]
    public async Task BrowserTabNavigation_emitsPortUrl_whenPortSubTabSelected()
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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());

        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserTabNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();

        // Auto-selects the first app and its first port; selecting it again keeps this assertion explicit.
        var portTab = vm.SubTabs.OfType<PortSubTabViewModel>().First();
        vm.SelectedSubTab = portTab;

        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == $"http://localhost:{port}/"),
            "Selecting the port sub-tab must emit the workspace id and the port's HTTP URL");
    }

    [Test]
    public async Task BrowserTabNavigation_fallsBackToPortUrl_whenAddressBarShowsChromeError()
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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserTabNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();

        // Simulate the headless browser reporting a chrome error (app was down, then workspace restarted).
        vm.UpdateAddressFromBrowser("ws-1", "chrome-error://chromewebdata/");

        // Deselect then re-select the port tab — mimics the port-open transition triggering navigation.
        var portTab = vm.SubTabs.OfType<PortSubTabViewModel>().First();
        vm.SelectedSubTab = null;
        vm.SelectedSubTab = portTab;

        Assert.Multiple(() =>
        {
            Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
                e => e.ws == "ws-1" && e.url == $"http://localhost:{port}/"),
                "Navigation must fall back to the port URL when the address bar shows a chrome error");
            Assert.That(emissions, Has.None.Matches<(string? ws, string? url)>(
                e => e.url == "chrome-error://chromewebdata/"),
                "Chrome error URL must never be passed as a navigation target");
        });
    }

    [Test]
    public async Task BrowserTabNavigation_doesNotReemitPortUrl_whenReturningFromConsoleToSamePort()
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
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserTabNavigation.Subscribe(e => emissions.Add(e));

        await vm.InitializeAsync();

        var portTab = vm.SubTabs.OfType<PortSubTabViewModel>().First();
        var consoleTab = vm.SubTabs.OfType<ConsoleSubTabViewModel>().Single();
        vm.SelectedSubTab = consoleTab;
        emissions.Clear();

        vm.SelectedSubTab = portTab;

        Assert.That(emissions, Has.None.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == $"http://localhost:{port}/"));
    }

    [Test]
    public async Task SelectedWorkspaceApplicationStateChange_refreshesApplicationPanel()
    {
        var dto = new WorkspaceDto("ws-1", "Workspace", "/repo", "/worktree", "main", "abc", "Starting")
        {
            Applications = [new ApplicationDto("Web", "npm run dev", null, "Starting")]
        };
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());

        await vm.InitializeAsync();
        vm.Sidebar.SelectedWorkspace!.ApplyStateChange("Running", [new AppStateChangeDto("Web", "Running")]);

        Assert.That(vm.Applications.SelectedApplication!.State, Is.EqualTo("Running"));
    }

    [Test]
    public async Task SelectedWorkspaceApplicationStateChange_emitsActiveBrowserNavigation()
    {
        var dto = new WorkspaceDto("ws-1", "Workspace", "/repo", "/worktree", "main", "abc", "Starting")
        {
            Applications =
            [
                new ApplicationDto("Web", "npm run dev", null, "Starting")
                {
                    AllocatedPorts = [new PortMappingDto(null, 3000, 10400)]
                }
            ]
        };
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(emissions.Add);

        await vm.InitializeAsync();
        emissions.Clear();

        vm.Sidebar.SelectedWorkspace!.ApplyStateChange("Running", [new AppStateChangeDto("Web", "Running")]);

        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == "http://localhost:10400/"));
    }

    [Test]
    public async Task SelectedWorkspaceApplicationStateChange_emitsActiveBrowserNavigation_whenConsoleTabSelected()
    {
        var dto = new WorkspaceDto("ws-1", "Workspace", "/repo", "/worktree", "main", "abc", "Starting")
        {
            Applications =
            [
                new ApplicationDto("Web", "npm run dev", null, "Starting")
                {
                    AllocatedPorts = [new PortMappingDto(null, 3000, 10400)]
                }
            ]
        };
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([dto]), NullConsoleClient());
        var emissions = new List<(string? WorkspaceId, string? Url)>();
        vm.BrowserNavigation.Subscribe(emissions.Add);

        await vm.InitializeAsync();
        vm.SelectedSubTab = vm.SubTabs.OfType<ConsoleSubTabViewModel>().Single();
        emissions.Clear();

        vm.Sidebar.SelectedWorkspace!.ApplyStateChange("Running", [new AppStateChangeDto("Web", "Running")]);

        Assert.That(emissions, Has.Some.Matches<(string? ws, string? url)>(
            e => e.ws == "ws-1" && e.url == "http://localhost:10400/"),
            "Headless browser must reconnect even when the console tab is currently shown");
    }

    [Test]
    public async Task SelectApplicationForUrl_doesNotSwitchWorkspaceForBrowserActivityInAnotherWorkspace()
    {
        var first = new WorkspaceDto("ws-1", "First", "/repo/first", "/worktrees/first", "main", "abc", "Running")
        {
            Applications =
            [
                new ApplicationDto("Web", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 5101, 5101)]
                },
                new ApplicationDto("Api", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 5102, 5102)]
                }
            ]
        };
        var second = new WorkspaceDto("ws-2", "Second", "/repo/second", "/worktrees/second", "main", "abc", "Running")
        {
            Applications =
            [
                new ApplicationDto("Docs", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 5201, 5201)]
                },
                new ApplicationDto("Admin", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 5202, 5202)]
                }
            ]
        };
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([first, second]), NullConsoleClient());

        await vm.InitializeAsync();
        var selected = vm.SelectApplicationForUrl("ws-2", "http://localhost:5202/users");

        Assert.Multiple(() =>
        {
            Assert.That(selected, Is.True);
            Assert.That(vm.Sidebar.SelectedWorkspace!.Id, Is.EqualTo("ws-1"));
            Assert.That(vm.Applications.SelectedApplication!.Name, Is.EqualTo("Web"));
            Assert.That(vm.SelectedSubTab, Is.TypeOf<PortSubTabViewModel>());
            Assert.That(((PortSubTabViewModel)vm.SelectedSubTab!).AllocatedPort, Is.EqualTo(5101));
            Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:5101/"));
        });
    }

    [Test]
    public async Task SelectApplicationForUrl_switchesApplicationOnlyInsideSelectedWorkspace()
    {
        var workspace = new WorkspaceDto("ws-1", "Workspace", "/repo/first", "/worktrees/first", "main", "abc", "Running")
        {
            Applications =
            [
                new ApplicationDto("Web", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 5101, 5101)]
                },
                new ApplicationDto("Api", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 5102, 5102)]
                }
            ]
        };
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([workspace]), NullConsoleClient());

        await vm.InitializeAsync();
        var selected = vm.SelectApplicationForUrl("ws-1", "http://localhost:5102/users");

        Assert.Multiple(() =>
        {
            Assert.That(selected, Is.True);
            Assert.That(vm.Sidebar.SelectedWorkspace!.Id, Is.EqualTo("ws-1"));
            Assert.That(vm.Applications.SelectedApplication!.Name, Is.EqualTo("Api"));
            Assert.That(vm.SelectedSubTab, Is.TypeOf<PortSubTabViewModel>());
            Assert.That(((PortSubTabViewModel)vm.SelectedSubTab!).AllocatedPort, Is.EqualTo(5102));
            Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:5102/users"));
        });
    }

    [Test]
    public async Task SelectApplicationForUrl_keepsSelectedWorkspace_WhenTargetPortIsUnknown()
    {
        var first = new WorkspaceDto("ws-1", "First", "/repo/first", "/worktrees/first", "main", "abc", "Running")
        {
            Applications =
            [
                new ApplicationDto("Web", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 5101, 5101)]
                }
            ]
        };
        var second = new WorkspaceDto("ws-2", "Second", "/repo/second", "/worktrees/second", "main", "abc", "Running")
        {
            Applications =
            [
                new ApplicationDto("Docs", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 5201, 5201)]
                }
            ]
        };
        var vm = MainViewModelFactory.Create(FakeWorkspaceClient([first, second]), NullConsoleClient());

        await vm.InitializeAsync();
        var selected = vm.SelectApplicationForUrl("ws-2", "http://localhost:5999/users");

        Assert.Multiple(() =>
        {
            Assert.That(selected, Is.False);
            Assert.That(vm.Sidebar.SelectedWorkspace!.Id, Is.EqualTo("ws-1"));
            Assert.That(vm.Applications.SelectedApplication!.Name, Is.EqualTo("Web"));
            Assert.That(vm.AddressBarUrl, Is.EqualTo("http://localhost:5101/"));
        });
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
