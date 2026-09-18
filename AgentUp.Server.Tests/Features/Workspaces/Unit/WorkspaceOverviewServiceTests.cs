using AgentUp.Server.Features.Processes.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceOverviewServiceTests
{
    [Test]
    public void Get_returnsNull_whenWorkspaceIsUnknown()
    {
        var service = CreateService(ServerTestComposition.CreateRegistry(), new FixedRuntimeProcessManager(), 0);

        Assert.That(service.Get("missing"), Is.Null);
    }

    [Test]
    public async Task Get_reportsTheWorkspaceIdentityTheRegistryHolds()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(Demo());
        var service = CreateService(registry, new FixedRuntimeProcessManager(), StorageBytes);

        var overview = service.Get(workspace.Id);

        Assert.That(overview, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(overview!.Id, Is.EqualTo(workspace.Id));
            Assert.That(overview.DisplayName, Is.EqualTo("Demo"));
            Assert.That(overview.RepositoryPath, Is.EqualTo(DemoRepositoryPath));
            Assert.That(overview.WorktreePath, Is.EqualTo(DemoWorktreePath));
            Assert.That(overview.Branch, Is.EqualTo(ServerDomain.Branch));
            Assert.That(overview.Commit, Is.EqualTo(DemoCommit));
        });
    }

    [Test]
    public async Task Get_combinesDiskUsageWithTheProcessManagersRuntime()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(Demo());
        var service = CreateService(registry, new FixedRuntimeProcessManager(), StorageBytes);

        var overview = service.Get(workspace.Id);

        Assert.Multiple(() =>
        {
            Assert.That(overview!.State, Is.EqualTo("Stopped"));
            Assert.That(overview.CpuPercent, Is.EqualTo(12.5));
            Assert.That(overview.MemoryBytes, Is.EqualTo(2048));
            Assert.That(overview.StorageBytes, Is.EqualTo(StorageBytes));
            Assert.That(overview.ProcessCount, Is.EqualTo(2));
            Assert.That(overview.ApplicationCount, Is.EqualTo(0));
        });
    }

    private const string DemoRepositoryPath = "/repos/app";
    private const string DemoWorktreePath = "/repos/app/.worktrees/demo";
    private const string DemoCommit = "abc123";
    private const long StorageBytes = 4096;

    private static RegisterWorkspaceRequest Demo()
        => ServerDomain.Workspace()
            .Named("Demo")
            .WithRepositoryPath(DemoRepositoryPath)
            .WithWorktreePath(DemoWorktreePath)
            .AtCommit(DemoCommit)
            .Build();

    private static WorkspaceOverviewService CreateService(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processes,
        long storageBytes)
        => new(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateProcessesController(processes),
            new FixedDiskUsageProvider(storageBytes));

    private sealed class FixedDiskUsageProvider(long bytes) : IWorkspaceDiskUsageProvider
    {
        public long Measure(string path) => bytes;
    }

    private sealed class FixedRuntimeProcessManager : IWorkspaceProcessManager
    {
        public Task LaunchAsync(Workspace workspace) => Task.CompletedTask;
        public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;
        public Task KillAsync(string workspaceId) => Task.CompletedTask;
        public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
        public WorkspaceRuntimeSnapshot GetRuntime(string workspaceId) => new(12.5, 2048, 2);
    }
}
