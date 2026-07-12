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
        var vm = new MainViewModel(NullClient());
        Assert.That(vm.SidebarWidth, Is.EqualTo(220));
    }

    [Test]
    public void SidebarWidth_is56WhenCollapsed()
    {
        var vm = new MainViewModel(NullClient());
        vm.IsSidebarCollapsed = true;
        Assert.That(vm.SidebarWidth, Is.EqualTo(56));
    }

    [Test]
    public void IsSidebarExpanded_invertsIsSidebarCollapsed()
    {
        var vm = new MainViewModel(NullClient());

        Assert.That(vm.IsSidebarExpanded, Is.True);
        vm.IsSidebarCollapsed = true;
        Assert.That(vm.IsSidebarExpanded, Is.False);
        vm.IsSidebarCollapsed = false;
        Assert.That(vm.IsSidebarExpanded, Is.True);
    }

    [Test]
    public void SidebarToggleIcon_changesWithCollapsedState()
    {
        var vm = new MainViewModel(NullClient());
        Assert.That(vm.SidebarToggleIcon, Is.EqualTo("‹"));
        vm.IsSidebarCollapsed = true;
        Assert.That(vm.SidebarToggleIcon, Is.EqualTo("›"));
    }

    [Test]
    public async Task InitializeAsync_setsErrorMessage_whenServerUnreachable()
    {
        var vm = new MainViewModel(NullClient());
        await vm.InitializeAsync();

        Assert.That(vm.ErrorMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public async Task InitializeAsync_populatesWorkspaces_onSuccess()
    {
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "feat/x", "abc123", "Stopped");
        var vm = new MainViewModel(FakeClient([dto]));

        await vm.InitializeAsync();

        Assert.That(vm.Workspaces, Has.Count.EqualTo(1));
        Assert.That(vm.Workspaces[0].DisplayName, Is.EqualTo("My App"));
        Assert.That(vm.ErrorMessage, Is.Null);
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public async Task InitializeAsync_selectsFirstWorkspace_automaticallyOnFirstLoad()
    {
        var dto = new WorkspaceDto("ws-1", "My App", "/repo", "/worktree", "feat/x", "abc123", "Stopped");
        var vm = new MainViewModel(FakeClient([dto]));

        await vm.InitializeAsync();

        Assert.That(vm.SelectedWorkspace, Is.Not.Null);
        Assert.That(vm.SelectedWorkspace!.Id, Is.EqualTo("ws-1"));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static WorkspaceApiClient NullClient()
    {
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:0") };
        return new WorkspaceApiClient(http);
    }

    private static WorkspaceApiClient FakeClient(List<WorkspaceDto> workspaces)
    {
        var handler = new FakeHttpMessageHandler(workspaces);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        return new WorkspaceApiClient(http);
    }
}

