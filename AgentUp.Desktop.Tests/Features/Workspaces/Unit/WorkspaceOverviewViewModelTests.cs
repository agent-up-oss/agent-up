using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceOverviewViewModelTests
{
    [Test]
    public void FormatBytes_usesTheSmallestReadableUnit()
    {
        Assert.That(WorkspaceOverviewViewModel.FormatBytes(512), Is.EqualTo("512 B"));
        Assert.That(WorkspaceOverviewViewModel.FormatBytes(2048), Is.EqualTo("2 KB"));
        Assert.That(WorkspaceOverviewViewModel.FormatBytes(5 * 1024 * 1024), Is.EqualTo("5 MB"));
    }

    [Test]
    public async Task LoadAsync_formatsServerOverviewMetrics()
    {
        var view = new WorkspaceOverviewViewModel(new WorkspacesController(new WorkspaceListService(new OverviewApiFake())));

        await view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.DisplayName, Is.EqualTo("Agent Up"));
            Assert.That(view.State, Is.EqualTo("Running"));
            Assert.That(view.Cpu, Is.EqualTo("12.5%"));
            Assert.That(view.Memory, Is.EqualTo("2 KB"));
            Assert.That(view.Storage, Is.EqualTo("5 MB"));
            Assert.That(view.ProcessCount, Is.EqualTo("3"));
            Assert.That(view.ApplicationCount, Is.EqualTo("4"));
            Assert.That(view.Commit, Is.EqualTo("abcdef12"));
        });
    }

    [Test]
    public async Task LoadAsync_showsASkeletonAndClearsPreviousWorkspace_untilTheNextResponseArrives()
    {
        var hang = new TaskCompletionSource<WorkspaceOverviewDto?>();
        var fake = new OverviewApiFake();
        var view = new WorkspaceOverviewViewModel(new WorkspacesController(new WorkspaceListService(fake)));
        await view.LoadAsync("ws-1");
        fake.NextOverview = hang.Task;

        var pending = view.LoadAsync("ws-2");

        Assert.Multiple(() =>
        {
            Assert.That(view.ShowSkeleton, Is.True);
            Assert.That(view.DisplayName, Is.Empty);
            Assert.That(view.Cpu, Is.EqualTo("—"));
        });

        hang.SetResult(new WorkspaceOverviewDto(
            "ws-2",
            "Other",
            "/other",
            "/other",
            "topic",
            "ffffffffffff",
            "Stopped",
            1,
            1024,
            2048,
            1,
            2));
        await pending;

        Assert.Multiple(() =>
        {
            Assert.That(view.ShowSkeleton, Is.False);
            Assert.That(view.DisplayName, Is.EqualTo("Other"));
            Assert.That(view.Cpu, Is.EqualTo("1.0%"));
        });
    }

    [Test]
    public async Task LoadAsync_keepsCurrentMetrics_whileRefreshingTheSameWorkspace()
    {
        var hang = new TaskCompletionSource<WorkspaceOverviewDto?>();
        var fake = new OverviewApiFake();
        var view = new WorkspaceOverviewViewModel(new WorkspacesController(new WorkspaceListService(fake)));
        await view.LoadAsync("ws-1");
        fake.NextOverview = hang.Task;

        var pending = view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.ShowSkeleton, Is.False);
            Assert.That(view.DisplayName, Is.EqualTo("Agent Up"));
            Assert.That(view.Cpu, Is.EqualTo("12.5%"));
        });

        hang.SetResult(new WorkspaceOverviewDto(
            "ws-1",
            "Agent Up",
            "/repo",
            "/worktree",
            "main",
            "abcdef123456",
            "Running",
            20,
            4096,
            5 * 1024 * 1024,
            3,
            4));
        await pending;

        Assert.That(view.Cpu, Is.EqualTo("20.0%"));
    }

    private sealed class OverviewApiFake : IWorkspaceApiProvider
    {
        public Task<WorkspaceOverviewDto?>? NextOverview { get; set; }
        public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<List<WorkspaceDto>>([]);

        public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkspaceDto?>(null);

        public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            if (NextOverview is not null)
            {
                var pending = NextOverview;
                NextOverview = null;
                return pending.WaitAsync(cancellationToken);
            }

            return Task.FromResult<WorkspaceOverviewDto?>(new WorkspaceOverviewDto(
                workspaceId,
                "Agent Up",
                "/repo",
                "/worktree",
                "main",
                "abcdef123456",
                "Running",
                12.5,
                2048,
                5 * 1024 * 1024,
                3,
                4));
        }
    }
}
