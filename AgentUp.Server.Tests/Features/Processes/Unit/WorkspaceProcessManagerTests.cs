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
    public async Task LaunchApplicationAsync_ProvidesAllWorkspacePortVariables_ToLocalProcess()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var webPath = Path.Combine(root, "web");
        var apiPath = Path.Combine(root, "api");
        Directory.CreateDirectory(webPath);
        Directory.CreateDirectory(apiPath);
        try
        {
            var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", root, root, "main", "c1")
            {
                Applications =
                [
                    new ApplicationDefinition(
                        "Web",
                        "printf '%s|%s\\n' \"$WEB_PORT\" \"$API_PORT\"",
                        "web",
                        [new PortDeclaration("WEB_PORT", 5173)]),
                    new ApplicationDefinition(
                        "Api",
                        "printf '%s\\n' \"$API_PORT\"",
                        "api",
                        [new PortDeclaration("API_PORT", 3001)])
                ]
            });

            await _manager.LaunchApplicationAsync(workspace, "Web");

            var webPort = workspace.Applications.Single(app => app.Name == "Web").AllocatedPorts.Single().AllocatedPort;
            var apiPort = workspace.Applications.Single(app => app.Name == "Api").AllocatedPorts.Single().AllocatedPort;
            var lines = await WaitForOutputAsync(workspace.Id, "Web");
            Assert.That(lines, Has.Some.EqualTo($"{webPort}|{apiPort}"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private async Task<IReadOnlyList<string>> WaitForOutputAsync(string workspaceId, string appName)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var lines = await _output.GetAsync(workspaceId, appName);
            if (lines.Count > 0)
                return lines;

            await Task.Delay(50);
        }

        return await _output.GetAsync(workspaceId, appName);
    }
}
