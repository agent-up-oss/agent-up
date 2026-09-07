using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceLifecycleServiceTests
{
    [Test]
    public async Task StartAndStop_ForSameWorkspace_DoNotInterleave()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var created = await registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));
        var processes = new BlockingLaunchWorkspaceProcessManager();
        var lifecycle = ServerTestComposition.CreateWorkspaceLifecycleService(registry, processes);

        var startTask = lifecycle.StartAsync(created.Id);
        // Wait until StartAsync is actually inside its transition (past the Starting state
        // update, blocked in LaunchWorkspaceAsync) so StopAsync below queues behind it rather
        // than racing to run first.
        await processes.EnteredLaunch.Task;

        var stopTask = lifecycle.StopAsync(created.Id);

        // Give StopAsync a moment to attempt (and, before the fix, succeed at) acquiring
        // the workspace before the blocked Start is allowed to proceed.
        await Task.Delay(50);
        processes.ReleaseLaunch.SetResult();

        await Task.WhenAll(startTask, stopTask);

        // With Start and Stop serialized per workspace, Stop cannot run until Start's entire
        // transition (including marking the workspace Running) has completed, so it always
        // has the last word here. Without the fix this is a race: Stop can finish while Start
        // is still blocked, and Start's later completion then leaves the workspace Running.
        var workspace = registry.GetById(created.Id);
        Assert.That(workspace!.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public async Task Start_OnRunningWorkspace_RecreatesWorkspace()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var created = await registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));
        var processes = new CountingWorkspaceProcessManager();
        var lifecycle = ServerTestComposition.CreateWorkspaceLifecycleService(registry, processes);

        await lifecycle.StartAsync(created.Id);
        await lifecycle.StartAsync(created.Id);

        Assert.Multiple(() =>
        {
            Assert.That(processes.LaunchCount, Is.EqualTo(2));
            Assert.That(processes.KillCount, Is.EqualTo(1));
            Assert.That(registry.GetById(created.Id)!.State, Is.EqualTo(WorkspaceState.Running));
        });
    }

    private sealed class BlockingLaunchWorkspaceProcessManager : IWorkspaceProcessManager
    {
        public TaskCompletionSource EnteredLaunch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseLaunch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task LaunchAsync(Workspace workspace)
        {
            EnteredLaunch.SetResult();
            await ReleaseLaunch.Task;
        }

        public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;
        public Task KillAsync(string workspaceId) => Task.CompletedTask;
        public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
    }

    private sealed class CountingWorkspaceProcessManager : IWorkspaceProcessManager
    {
        public int LaunchCount { get; private set; }
        public int KillCount { get; private set; }

        public Task LaunchAsync(Workspace workspace)
        {
            LaunchCount++;
            return Task.CompletedTask;
        }

        public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;

        public Task KillAsync(string workspaceId)
        {
            KillCount++;
            return Task.CompletedTask;
        }

        public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
    }
}
