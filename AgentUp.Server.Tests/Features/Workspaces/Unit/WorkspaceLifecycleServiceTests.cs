using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceLifecycleServiceTests
{
    [Test]
    public async Task StartAndStop_ForSameWorkspace_DoNotInterleave()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var created = await registry.RegisterAsync(ServerDomain.Workspace().Build());
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
        var created = await registry.RegisterAsync(ServerDomain.Workspace().Build());
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

    [Test]
    public async Task Start_marksDesktopApplicationsFailedOffLinuxWithoutFailingTheWorkspace()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var created = await registry.RegisterAsync(ServerDomain.Workspace()
            .WithDesktopApplication(new DesktopApplicationDefinition("Editor", "dotnet run", "."))
            .Build());
        var processes = new CountingWorkspaceProcessManager();
        var lifecycle = ServerTestComposition.CreateWorkspaceLifecycleService(registry, processes, isLinux: () => false);

        var result = await lifecycle.StartAsync(created.Id);
        var workspace = registry.GetById(created.Id)!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(workspace.State, Is.EqualTo(WorkspaceState.Running));
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(workspace.Applications.Single().RuntimeEnvironment, Is.Empty);
        });
    }

    [Test]
    public async Task Start_preparesDesktopApplicationsOnLinux()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var created = await registry.RegisterAsync(ServerDomain.Workspace()
            .WithDesktopApplication(new DesktopApplicationDefinition("Editor", "dotnet run", "."))
            .Build());
        var processes = new CountingWorkspaceProcessManager();
        var lifecycle = ServerTestComposition.CreateWorkspaceLifecycleService(registry, processes, isLinux: () => true);

        var result = await lifecycle.StartAsync(created.Id);
        var workspace = registry.GetById(created.Id)!;
        var application = workspace.Applications.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(workspace.State, Is.EqualTo(WorkspaceState.Running));
            Assert.That(application.State, Is.EqualTo(ApplicationState.Running));
            Assert.That(application.RuntimeEnvironment["DISPLAY"], Is.EqualTo(":123"));
            Assert.That(application.RuntimeEnvironment["LD_LIBRARY_PATH"], Is.EqualTo("/nix/store/fake-fontconfig/lib"));
        });

        await lifecycle.StopAsync(created.Id);
    }

    [Test]
    public async Task Start_stopsDesktopSessionsWhenLaunchFails()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var created = await registry.RegisterAsync(ServerDomain.Workspace()
            .WithDesktopApplication(new DesktopApplicationDefinition("Editor", "dotnet run", "."))
            .Build());
        var lifecycle = ServerTestComposition.CreateWorkspaceLifecycleService(
            registry,
            new ThrowingLaunchWorkspaceProcessManager(),
            isLinux: () => true);

        var result = await lifecycle.StartAsync(created.Id);
        var workspace = registry.GetById(created.Id)!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(workspace.State, Is.EqualTo(WorkspaceState.Failed));
            Assert.That(workspace.LastError, Is.EqualTo("launch exploded"));
        });
    }

    private sealed class ThrowingLaunchWorkspaceProcessManager : IWorkspaceProcessManager
    {
        public Task LaunchAsync(Workspace workspace) => throw new InvalidOperationException("launch exploded");
        public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;
        public Task KillAsync(string workspaceId) => Task.CompletedTask;
        public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
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
