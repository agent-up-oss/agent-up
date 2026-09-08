using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

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

    private sealed class FakeWorkspaceApiProvider : IWorkspaceApiProvider
    {
        public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<List<WorkspaceDto>>([]);

        public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkspaceDto?>(null);

        public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceDto("ws-cloned", "widgets", "/clones/widgets", "/clones/widgets", "main", "abc123", "Stopped"));

        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
