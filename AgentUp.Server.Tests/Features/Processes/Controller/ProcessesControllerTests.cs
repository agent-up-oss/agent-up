using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Processes.Controller;

[TestFixture]
public sealed class ProcessesControllerTests
{
    [Test]
    public async Task Workspace_and_application_lifecycle_calls_reach_the_process_manager()
    {
        var manager = new RecordingProcessManager();
        var controller = new ProcessesController(manager, new ProcessOutputService(new InMemoryOutputRepository()));
        var workspace = WorkspaceOf("one");

        await controller.LaunchWorkspaceAsync(workspace);
        await controller.LaunchApplicationAsync(workspace, "web");
        await controller.KillApplicationAsync("one", "web");
        await controller.KillWorkspaceAsync("one");

        Assert.That(manager.Actions,
            Is.EqualTo(new[] { "launch:one", "launch:one:web", "kill:one:web", "kill:one" }));
    }

    [Test]
    public async Task Output_and_runtime_queries_return_manager_owned_state()
    {
        var output = new InMemoryOutputRepository();
        await output.AppendAsync("one", "web", "ready");
        var manager = new RecordingProcessManager();
        var controller = new ProcessesController(manager, new ProcessOutputService(output));

        Assert.That(await controller.GetOutputAsync("one", "web"), Is.EqualTo(new[] { "ready" }));
        Assert.That(controller.GetRuntime("one"), Is.EqualTo(WorkspaceRuntimeSnapshot.Empty));
    }

    private sealed class RecordingProcessManager : IWorkspaceProcessManager
    {
        public List<string> Actions { get; } = [];
        public Task LaunchAsync(Workspace workspace) { Actions.Add($"launch:{workspace.Id}"); return Task.CompletedTask; }
        public Task LaunchApplicationAsync(Workspace workspace, string appName) { Actions.Add($"launch:{workspace.Id}:{appName}"); return Task.CompletedTask; }
        public Task KillAsync(string workspaceId) { Actions.Add($"kill:{workspaceId}"); return Task.CompletedTask; }
        public Task KillApplicationAsync(string workspaceId, string appName) { Actions.Add($"kill:{workspaceId}:{appName}"); return Task.CompletedTask; }
    }

    private static Workspace WorkspaceOf(string id) => new()
    {
        Id = id,
        DisplayName = id,
        RepositoryPath = "/repo",
        WorktreePath = "/repo",
        Branch = "main",
        Commit = "abc"
    };
}
