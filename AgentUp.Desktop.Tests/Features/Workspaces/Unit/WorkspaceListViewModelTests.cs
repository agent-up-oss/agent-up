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

    [Test]
    public async Task RefreshDerivedWorkspaceAsync_reloadsTheWorkspaceAfterASuccessfulRefresh()
    {
        var workspace = DesktopDomain.Workspace()
            .WithId("review")
            .Named("Review · topic")
            .Stopped()
            .AsDerived("https://example.test/acme/widgets.git", "topic")
            .Build();
        var api = new RefreshingWorkspaceApi(workspace);
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(api)));
        sidebar.Workspaces.Add(new WorkspaceItemViewModel(
            workspace.Id,
            workspace.DisplayName,
            workspace.Branch,
            workspace.RepositoryPath,
            workspace.WorktreePath,
            workspace.State,
            derived: true,
            sourceRevision: "topic"));

        ((IWorkspaceItemHost)sidebar).ShowRefreshDerivedWorkspace(workspace.Id, "topic");

        Assert.That(sidebar.RefreshReview.IsVisible, Is.True);
        Assert.That(sidebar.RefreshReview.Revision, Is.EqualTo("topic"));

        await sidebar.RefreshDerivedWorkspaceAsync(workspace.Id, "release");

        Assert.That(api.RefreshedId, Is.EqualTo(workspace.Id));
        Assert.That(api.RefreshedRevision, Is.EqualTo("release"));
        Assert.That(sidebar.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task RefreshDerivedWorkspaceAsync_swallowsCallerCancellation()
    {
        var api = new RefreshingWorkspaceApi(DesktopDomain.Workspace().Build()) { CancelRefresh = true };
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(api)));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await sidebar.RefreshDerivedWorkspaceAsync("review", ct: cancellation.Token);

        Assert.That(sidebar.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task RefreshDerivedWorkspaceAsync_reportsStructuredFailures()
    {
        var api = new RefreshingWorkspaceApi(DesktopDomain.Workspace().Build())
        {
            RefreshError = new InvalidOperationException("only derived")
        };
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(api)));

        await sidebar.RefreshDerivedWorkspaceAsync("review");

        Assert.That(sidebar.ErrorMessage, Is.EqualTo("Could not refresh review workspace: only derived"));
    }

    private sealed class RefreshingWorkspaceApi(WorkspaceDto workspace) : IWorkspaceApiProvider
    {
        public string? RefreshedId { get; private set; }

        public string? RefreshedRevision { get; private set; }

        public bool CancelRefresh { get; init; }

        public Exception? RefreshError { get; init; }

        public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<WorkspaceDto> { workspace });

        public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceDto?>(workspace.Id == workspaceId ? workspace : null);

        public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(workspace);

        public Task<WorkspaceDto> RefreshCloneAsync(string workspaceId, string? revision = null, CancellationToken cancellationToken = default)
        {
            if (CancelRefresh)
                return Task.FromCanceled<WorkspaceDto>(cancellationToken);
            if (RefreshError is not null)
                return Task.FromException<WorkspaceDto>(RefreshError);
            RefreshedId = workspaceId;
            RefreshedRevision = revision;
            return Task.FromResult(workspace);
        }

        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceOverviewDto?>(null);
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

        public Task<WorkspaceDto> RefreshCloneAsync(string workspaceId, string? revision = null, CancellationToken cancellationToken = default)
            => GetByIdAsync(workspaceId, cancellationToken).ContinueWith(task => task.Result ?? throw new InvalidOperationException("missing"), cancellationToken);

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

        public Task<WorkspaceDto> RefreshCloneAsync(string workspaceId, string? revision = null, CancellationToken cancellationToken = default)
            => GetByIdAsync(workspaceId, cancellationToken).ContinueWith(task => task.Result ?? throw new InvalidOperationException("missing"), cancellationToken);

        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkspaceOverviewDto?>(null);
    }
}
