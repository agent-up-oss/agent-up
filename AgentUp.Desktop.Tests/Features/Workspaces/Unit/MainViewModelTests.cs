using AgentUp.Desktop.Features.Applications.Http;
using AgentUp.Desktop.Features.Console.Http;
using AgentUp.Desktop.Features.Ports.Http;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Http;
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

        // Auto-selects first app → Console sub-tab; now explicitly select the port sub-tab.
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
}
