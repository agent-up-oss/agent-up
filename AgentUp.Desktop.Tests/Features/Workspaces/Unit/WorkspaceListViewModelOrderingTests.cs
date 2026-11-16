using System.Collections.Specialized;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public class WorkspaceListViewModelOrderingTests
{
    [Test]
    public void ApplyEvent_keepsStoppedWorkspaceBelowRunningDuringStopTransition()
    {
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(new FakeWorkspaceApiProvider())));
        sidebar.Workspaces.Add(new WorkspaceItemViewModel(
            "ws-1", "agent1", "main", "/repo/agent1", "/worktrees/agent1", "Stopping"));
        sidebar.Workspaces.Add(new WorkspaceItemViewModel(
            "ws-2", "agent2", "main", "/repo/agent2", "/worktrees/agent2", "Running"));

        sidebar.ApplyEvent("ws-1", "Stopping", [], null);

        Assert.That(sidebar.Workspaces.Select(w => w.Id).ToList(), Is.EqualTo(["ws-2", "ws-1"]));
    }

    [Test]
    public void ApplyEvent_keepsTheOpenWorkspaceSelectedWhenTheOrderChanges()
    {
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(new FakeWorkspaceApiProvider())));
        sidebar.Workspaces.Add(new WorkspaceItemViewModel(
            "ws-1", "agent1", "main", "/repo/agent1", "/worktrees/agent1", "Stopping"));
        sidebar.Workspaces.Add(new WorkspaceItemViewModel(
            "ws-2", "agent2", "main", "/repo/agent2", "/worktrees/agent2", "Running"));
        sidebar.SelectedWorkspace = sidebar.Workspaces[0];
        // Stand in for the bound list control, which is what makes this worth guarding: clearing
        // the collection makes it drop its selection and write that null straight back. Without
        // this the test passes against the clear-and-repopulate code it exists to rule out.
        sidebar.Workspaces.CollectionChanged += (_, change) =>
        {
            if (change.Action == NotifyCollectionChangedAction.Reset) sidebar.SelectedWorkspace = null;
        };

        sidebar.ApplyEvent("ws-1", "Stopping", [], null);

        Assert.That(sidebar.Workspaces.Select(w => w.Id).ToList(), Is.EqualTo(["ws-2", "ws-1"]));
        Assert.That(sidebar.SelectedWorkspace?.Id, Is.EqualTo("ws-1"));
    }

    [Test]
    public async Task LoadAsync_keepsOwnWorkspacesAboveDerivedReviewWorkspaces()
    {
        var api = new FakeWorkspaceApiProvider
        {
            Workspaces =
            [
                DesktopDomain.Workspace()
                    .WithId("review")
                    .Named("Review · topic")
                    .OnBranch("topic")
                    .Stopped()
                    .AsDerived("https://example.test/acme/widgets.git", "topic")
                    .Build(),
                DesktopDomain.Workspace()
                    .WithId("own")
                    .Named("widgets")
                    .Stopped()
                    .Build()
            ]
        };
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(api)));

        await sidebar.LoadAsync();

        Assert.That(sidebar.Workspaces.Select(w => w.Id).ToList(), Is.EqualTo(["own", "review"]));
        Assert.That(sidebar.Workspaces[1].Derived, Is.True);
        Assert.That(sidebar.Workspaces[1].Subtitle, Is.EqualTo("topic"));
    }

    private sealed class FakeWorkspaceApiProvider : IWorkspaceApiProvider
    {
        public List<WorkspaceDto> Workspaces { get; init; } = [];

        public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Workspaces);

        public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkspaceDto?>(null);

        public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(DesktopDomain.Workspace()
                .WithId("ws-cloned")
                .Named("widgets")
                .WithRepositoryPath("/clones/widgets")
                .WithWorktreePath("/clones/widgets")
                .OnBranch("main")
                
                .Stopped()
                .Build());

        public Task<WorkspaceDto> RefreshCloneAsync(string workspaceId, string? revision = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(DesktopDomain.Workspace().WithId(workspaceId).Stopped().Build());

        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkspaceOverviewDto?>(null);

        public Task<string?> GetReviewReportHtmlAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
