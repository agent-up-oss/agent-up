using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Controller;

[TestFixture]
public sealed class WorkspacesControllerTests
{
    [Test]
    public async Task CloneAsync_builds_the_source_request_at_the_controller_boundary()
    {
        var provider = new RecordingWorkspaceProvider();
        var controller = new WorkspacesController(new WorkspaceListService(provider));

        var result = await controller.CloneAsync("https://example.test/repo.git", "feature/test");

        Assert.That(result.Id, Is.EqualTo("cloned"));
        Assert.That(provider.CloneRequest,
            Is.EqualTo(new CloneSourceRequestDto("https://example.test/repo.git", "feature/test")));
    }

    [Test]
    public async Task CloneAsync_marksAReviewWorkspaceDerived()
    {
        var provider = new RecordingWorkspaceProvider();
        var controller = new WorkspacesController(new WorkspaceListService(provider));

        await controller.CloneAsync("https://example.test/repo.git", "abc1234", derived: true);

        Assert.That(provider.CloneRequest, Is.EqualTo(new CloneSourceRequestDto("https://example.test/repo.git", "abc1234")
        {
            Derived = true,
            Revision = "abc1234"
        }));
    }

    [Test]
    public async Task Lifecycle_actions_forward_the_workspace_identity()
    {
        var provider = new RecordingWorkspaceProvider();
        var controller = new WorkspacesController(new WorkspaceListService(provider));

        await controller.StartAsync("one");
        await controller.StopAsync("two");
        await controller.DeleteAsync("three");
        await controller.RefreshCloneAsync("four");

        Assert.That(provider.Actions, Is.EqualTo(new[] { "start:one", "stop:two", "delete:three", "refresh:four" }));
    }

    private sealed class RecordingWorkspaceProvider : IWorkspaceApiProvider
    {
        public CloneSourceRequestDto? CloneRequest { get; private set; }
        public List<string> Actions { get; } = [];
        public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<List<WorkspaceDto>>([]);
        public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.FromResult<WorkspaceDto?>(null);
        public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.FromResult<WorkspaceOverviewDto?>(null);
        public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default)
        {
            CloneRequest = request;
            return Task.FromResult(DesktopDomain.Workspace()
                .WithId("cloned")
                .Named("clone")
                .WithRepositoryPath("/repo")
                .WithWorktreePath("/repo")
                .OnBranch("main")
                .AtCommit("abc")
                .InState("stopped")
                .Build());
        }
        public Task<WorkspaceDto> RefreshCloneAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            Actions.Add($"refresh:{workspaceId}");
            return Task.FromResult(DesktopDomain.Workspace().WithId(workspaceId).Build());
        }
        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) { Actions.Add($"start:{workspaceId}"); return Task.CompletedTask; }
        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) { Actions.Add($"stop:{workspaceId}"); return Task.CompletedTask; }
        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) { Actions.Add($"delete:{workspaceId}"); return Task.CompletedTask; }
    }
}
