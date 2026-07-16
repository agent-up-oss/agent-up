using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Ports.Models;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Processes.Unit;

[TestFixture]
public class WorkspaceProcessManagerTests
{
    private InMemoryOutputRepository _output = null!;
    private WorkspaceRegistry _registry = null!;
    private WorkspaceProcessManager _manager = null!;

    [SetUp]
    public async Task SetUp()
    {
        _output = new InMemoryOutputRepository();
        _registry = new WorkspaceRegistry(new InMemoryWorkspaceRepository(), new InMemoryPortAllocationService());
        await ((IHostedService)_registry).StartAsync(CancellationToken.None);
        _manager = new WorkspaceProcessManager(_registry, _output, NullLogger<WorkspaceProcessManager>.Instance);
    }

    [Test]
    public async Task LaunchDockerService_WritesStderr_ToOutputRepository_OnStartupFailure()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Services = [new DockerServiceDefinition("Db", "agent-up-nonexistent-image-xyz:latest")]
        });

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.LaunchApplicationAsync(workspace, "Db"));

        var lines = await _output.GetAsync(workspace.Id, "Db");
        Assert.That(lines, Is.Not.Empty);
        Assert.That(lines, Has.Some.StartsWith("[err]"));
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_ProvidesAllWorkspacePortVariables_ToLocalProcess()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/repo", "/repo/worktree", "main", "c1")
        {
            Applications =
            [
                new ApplicationDefinition(
                    "Web",
                    "npm run dev",
                    "web",
                    [new PortDeclaration("WEB_PORT", 5173)]),
                new ApplicationDefinition(
                    "Api",
                    "dotnet run",
                    "api",
                    [new PortDeclaration("API_PORT", 3001)])
            ]
        });

        var web = workspace.Applications.Single(app => app.Name == "Web");
        var api = workspace.Applications.Single(app => app.Name == "Api");

        var startInfo = WorkspaceProcessManager.CreateLocalProcessStartInfo(workspace, web);

        Assert.That(startInfo.WorkingDirectory, Is.EqualTo(Path.Join(workspace.WorktreePath, "web")));
        Assert.That(startInfo.Environment["WEB_PORT"], Is.EqualTo(web.AllocatedPorts.Single().AllocatedPort.ToString()));
        Assert.That(startInfo.Environment["API_PORT"], Is.EqualTo(api.AllocatedPorts.Single().AllocatedPort.ToString()));
    }
}
