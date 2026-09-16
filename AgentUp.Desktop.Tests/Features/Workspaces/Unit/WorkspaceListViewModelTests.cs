using System.Net;
using System.Net.Http;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceListViewModelTests
{
    [Test]
    public async Task LoadAsync_marksSignInRequiredOnUnauthorized()
    {
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(new UnauthorizedWorkspaceApi())));

        await sidebar.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sidebar.RequiresSignIn, Is.True);
            Assert.That(sidebar.ErrorMessage, Is.EqualTo("Sign in required."));
            Assert.That(sidebar.IsLoading, Is.False);
        });
    }

    [Test]
    public void Disconnect_clearsWorkspacesAndSignInState()
    {
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(new UnauthorizedWorkspaceApi())));
        sidebar.Workspaces.Add(new WorkspaceItemViewModel(
            "ws-1", "agent1", "main", "/repo", "/worktree", "Running"));
        sidebar.SelectedWorkspace = sidebar.Workspaces[0];

        sidebar.Disconnect();

        Assert.Multiple(() =>
        {
            Assert.That(sidebar.Workspaces, Is.Empty);
            Assert.That(sidebar.SelectedWorkspace, Is.Null);
            Assert.That(sidebar.ErrorMessage, Is.Null);
            Assert.That(sidebar.RequiresSignIn, Is.False);
            Assert.That(sidebar.IsLoading, Is.False);
        });
    }

    private sealed class UnauthorizedWorkspaceApi : IWorkspaceApiProvider
    {
        public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
            => throw new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized);

        public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceDto?>(null);

        public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkspaceDto("ws-cloned", "widgets", "/clones/widgets", "/clones/widgets", "main", "abc123", "Stopped"));

        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceOverviewDto?>(null);
    }
}
