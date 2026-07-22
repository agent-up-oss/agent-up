using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Models;
using AgentUp.Server.Features.Processes.Providers;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

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
        _registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new InMemoryPortAllocationService(),
            new CapabilityReconciliationService([]));
        await ((IHostedService)_registry).StartAsync(CancellationToken.None);
        _manager = new WorkspaceProcessManager(
            _registry,
            _output,
            new LocalProcessProvider(),
            new DockerProcessProvider(),
            NullLogger<WorkspaceProcessManager>.Instance);
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

        var startInfo = LocalProcessProvider.CreateStartInfo(workspace, web);

        Assert.That(startInfo.WorkingDirectory, Is.EqualTo(Path.Join(workspace.WorktreePath, "web")));
        Assert.That(startInfo.Environment["WEB_PORT"], Is.EqualTo(web.AllocatedPorts.Single().AllocatedPort.ToString()));
        Assert.That(startInfo.Environment["API_PORT"], Is.EqualTo(api.AllocatedPorts.Single().AllocatedPort.ToString()));
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_LoadsApplicationEnvironmentFilesAndInlineEnvironment()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        await File.WriteAllTextAsync(Path.Join(worktreePath, ".env"), """
            SECRET_PASSWORD=from-file
            SHARED_VALUE=from-file
            QUOTED_VALUE="with spaces"
            export EXPORTED_VALUE=true
            """);

        try
        {
            var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", worktreePath, worktreePath, "main", "c1")
            {
                Applications =
                [
                    new ApplicationDefinition(
                        "Web",
                        "printenv",
                        null,
                        [new PortDeclaration("WEB_PORT", 5173)],
                        new Dictionary<string, string>
                        {
                            ["SHARED_VALUE"] = "from-inline",
                            ["INLINE_ONLY"] = "true",
                            ["WEB_PORT"] = "from-inline"
                        },
                        [".env"])
                ]
            });

            var app = workspace.Applications.Single();
            var startInfo = LocalProcessProvider.CreateStartInfo(workspace, app);

            Assert.That(startInfo.Environment["SECRET_PASSWORD"], Is.EqualTo("from-file"));
            Assert.That(startInfo.Environment["SHARED_VALUE"], Is.EqualTo("from-inline"));
            Assert.That(startInfo.Environment["INLINE_ONLY"], Is.EqualTo("true"));
            Assert.That(startInfo.Environment["QUOTED_VALUE"], Is.EqualTo("with spaces"));
            Assert.That(startInfo.Environment["EXPORTED_VALUE"], Is.EqualTo("true"));
            Assert.That(startInfo.Environment["WEB_PORT"], Is.EqualTo(app.AllocatedPorts.Single().AllocatedPort.ToString()));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_RejectsEnvironmentFilesOutsideWorkspaceRoot()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/repo", "/repo/worktree", "main", "c1")
        {
            Applications =
            [
                new ApplicationDefinition(
                    "Web",
                    "printenv",
                    null,
                    null,
                    null,
                    ["../.env"])
            ]
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            LocalProcessProvider.CreateStartInfo(workspace, workspace.Applications.Single()));

        Assert.That(ex!.Message, Does.Contain("must stay under the workspace root"));
    }

    [Test]
    public async Task CreateDockerRunArguments_AddsEnvironmentFilesAndInlineEnvironment()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        await File.WriteAllTextAsync(Path.Join(worktreePath, ".env.database"), "POSTGRES_PASSWORD=secret");

        try
        {
            var app = new ApplicationInstance
            {
                Name = "Database",
                ServiceType = ServiceType.Docker,
                Image = "postgres:17",
                Environment = new Dictionary<string, string> { ["POSTGRES_USER"] = "user" },
                EnvironmentFiles = [".env.database"]
            };

            var args = new DockerProcessProvider().CreateRunArguments("agentup-test-db", app, worktreePath);

            Assert.That(args, Does.Contain("--env-file"));
            Assert.That(args, Does.Contain(Path.Join(worktreePath, ".env.database")));
            Assert.That(args, Does.Contain("-e"));
            Assert.That(args, Does.Contain("POSTGRES_USER=user"));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }
}
