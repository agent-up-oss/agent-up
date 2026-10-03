using System.Net;
using System.Net.Http;
using AgentUp.Desktop.Features.Entitlements.Controllers;
using AgentUp.Desktop.Features.Entitlements.Providers;
using AgentUp.Desktop.Features.Entitlements.Services;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

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

    [Test]
    public async Task LoadAsync_hidesAddWhenWorkspaceCreateIsUnavailable()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Json(new
        {
            displayName = "Team",
            features = new Dictionary<string, object> { ["workspace.create"] = new { available = false } },
            limits = new Dictionary<string, object>()
        }));
        var sidebar = new WorkspaceListViewModel(
            new WorkspacesController(new WorkspaceListService(new EmptyWorkspaceApi())),
            new EntitlementsController(new EntitlementsService(new EntitlementsApiClient(http.Client))));

        await sidebar.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sidebar.CanCreateWorkspace, Is.False);
            Assert.That(sidebar.Plan.DisplayName, Is.EqualTo("Team"));
            Assert.That(sidebar.EmptyStateHint, Does.Contain("does not allow adding workspaces"));
        });
    }

    [Test]
    public async Task LoadAsync_keepsAddWhenTheEntitlementDocumentIsMissing()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Empty(HttpStatusCode.NotFound));
        var sidebar = new WorkspaceListViewModel(
            new WorkspacesController(new WorkspaceListService(new EmptyWorkspaceApi())),
            new EntitlementsController(new EntitlementsService(new EntitlementsApiClient(http.Client))));

        await sidebar.LoadAsync();

        Assert.That(sidebar.CanCreateWorkspace, Is.True);
    }

    private sealed class EmptyWorkspaceApi : IWorkspaceApiProvider
    {
        public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<List<WorkspaceDto>>([]);

        public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceDto?>(null);

        public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(DesktopDomain.Workspace()
                .WithId("ws-cloned")
                .Named("widgets")
                .WithRepositoryPath("/clones/widgets")
                .WithWorktreePath("/clones/widgets")
                .OnBranch("main")
                .Stopped()
                .Build());

        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceOverviewDto?>(null);
    }

    private sealed class UnauthorizedWorkspaceApi : IWorkspaceApiProvider
    {
        public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
            => throw new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized);

        public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceDto?>(null);

        public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(DesktopDomain.Workspace()
                .WithId("ws-cloned")
                .Named("widgets")
                .WithRepositoryPath("/clones/widgets")
                .WithWorktreePath("/clones/widgets")
                .OnBranch("main")
                
                .Stopped()
                .Build());

        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceOverviewDto?>(null);
    }
}
