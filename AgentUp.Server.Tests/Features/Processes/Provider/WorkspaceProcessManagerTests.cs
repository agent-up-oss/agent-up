using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.DTOs;
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
        _registry = ServerTestComposition.CreateRegistry();
        await ((IHostedService)_registry).StartAsync(CancellationToken.None);
        _manager = new WorkspaceProcessManager(
            ServerTestComposition.CreateWorkspaceStateController(_registry),
            new ProcessOutputService(_output),
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
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Join(worktreePath, "web"));
        Directory.CreateDirectory(Path.Join(worktreePath, "api"));

        try
        {
            var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", worktreePath, worktreePath, "main", "c1")
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

            Assert.That(startInfo.WorkingDirectory, Is.Empty);
            Assert.That(startInfo.ArgumentList[0], Is.EqualTo("--prefix"));
            Assert.That(Directory.ResolveLinkTarget(startInfo.ArgumentList[1], returnFinalTarget: true)!.FullName,
                Is.EqualTo(Path.Join(workspace.WorktreePath, "web")));
            Assert.That(startInfo.Environment["WEB_PORT"], Is.EqualTo(web.AllocatedPorts.Single().AllocatedPort.ToString()));
            Assert.That(startInfo.Environment["API_PORT"], Is.EqualTo(api.AllocatedPorts.Single().AllocatedPort.ToString()));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
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
    public async Task CreateLocalProcessStartInfo_UsesValidatedExecutableAndArgumentList()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/repo", "/repo/worktree", "main", "c1")
        {
            Applications =
            [
                new ApplicationDefinition("Web", "npm run \"dev server\"", null)
            ]
        });

        var startInfo = LocalProcessProvider.CreateStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo.FileName, Is.EqualTo("npm"));
        Assert.That(startInfo.WorkingDirectory, Is.Empty);
        Assert.That(startInfo.ArgumentList[0], Is.EqualTo("--prefix"));
        Assert.That(Directory.ResolveLinkTarget(startInfo.ArgumentList[1], returnFinalTarget: true)!.FullName,
            Is.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList.Skip(2), Is.EqualTo(new[] { "run", "dev server" }));
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_RejectsShellExpressions()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/repo", "/repo/worktree", "main", "c1")
        {
            Applications =
            [
                new ApplicationDefinition("Web", "npm run dev; rm -rf /", null)
            ]
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            LocalProcessProvider.CreateStartInfo(workspace, workspace.Applications.Single()));

        Assert.That(ex!.Message, Does.Contain("not a shell expression"));
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
    public async Task KillApplicationAsync_marksIntentionalLocalProcessExitAsStopped()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Local process stop race coverage uses Linux process tools.");

        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", worktreePath, worktreePath, "main", "c1")
            {
                Applications =
                [
                    new ApplicationDefinition("Web", "python3 -m http.server 0 --bind 127.0.0.1", null)
                ]
            });

            await _manager.LaunchApplicationAsync(workspace, "Web");
            await _registry.UpdateApplicationStateAsync(workspace.Id, "Web", ApplicationState.Running);

            await _manager.KillApplicationAsync(workspace.Id, "Web");

            var state = await WaitForApplicationStateAsync(workspace.Id, "Web", ApplicationState.Stopped);
            Assert.That(state, Is.EqualTo(ApplicationState.Stopped));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task LaunchApplicationAsync_recordsFastNonZeroExitAsFailed()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", worktreePath, worktreePath, "main", "c1")
            {
                Applications =
                [
                    new ApplicationDefinition("Web", "python3 -c \"raise Exception\"", null)
                ]
            });

            await _manager.LaunchApplicationAsync(workspace, "Web");

            var state = await WaitForApplicationStateAsync(workspace.Id, "Web", ApplicationState.Failed);
            Assert.That(state, Is.EqualTo(ApplicationState.Failed));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateDockerRunArguments_AddsEnvironmentFilesAndInlineEnvironment()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        await File.WriteAllTextAsync(Path.Join(worktreePath, ".env.database"), "POSTGRES_PASSWORD=secret");

        try
        {
            var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", worktreePath, worktreePath, "main", "c1")
            {
                Services =
                [
                    new DockerServiceDefinition(
                        "Database",
                        "postgres:17",
                        Environment: new Dictionary<string, string> { ["POSTGRES_USER"] = "user" },
                        EnvironmentFiles: [".env.database"])
                ]
            });

            var args = new DockerProcessProvider().CreateRunArguments("agentup-test-db", workspace, workspace.Applications.Single());

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

    [Test]
    public async Task CreateDockerRunArguments_AddsHostGatewayAliasAndInterpolatesWorkspacePorts()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/repo", "/repo/worktree", "main", "c1")
        {
            Applications =
            [
                new ApplicationDefinition(
                    "Roommate",
                    "./gradlew bootRun",
                    null,
                    [new PortDeclaration("SERVER_PORT", 8080)])
            ],
            Services =
            [
                new DockerServiceDefinition(
                    "Keymaster",
                    "team-propra/keymaster:v1",
                    [new PortDeclaration("KEYMASTER_PORT", 3000)],
                    new Dictionary<string, string>
                    {
                        ["ROOMMATE_URL"] = "http://host.agent-up:${SERVER_PORT}",
                        ["ROOMMATE_ENDPOINT"] = "/api/access",
                        ["UNRESOLVED"] = "${MISSING_PORT}"
                    })
            ]
        });
        var keymaster = workspace.Applications.Single(app => app.Name == "Keymaster");
        var roommatePort = workspace.Applications
            .Single(app => app.Name == "Roommate")
            .AllocatedPorts.Single()
            .AllocatedPort;

        var args = new DockerProcessProvider().CreateRunArguments("agentup-test-keymaster", workspace, keymaster);

        Assert.That(args, Does.Contain("--add-host"));
        Assert.That(args, Does.Contain("host.agent-up:host-gateway"));
        Assert.That(args, Does.Contain($"ROOMMATE_URL=http://host.agent-up:{roommatePort}"));
        Assert.That(args, Does.Contain("ROOMMATE_ENDPOINT=/api/access"));
        Assert.That(args, Does.Contain("UNRESOLVED=${MISSING_PORT}"));
    }

    private async Task<ApplicationState> WaitForApplicationStateAsync(
        string workspaceId,
        string appName,
        ApplicationState expected)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        var state = _registry.GetById(workspaceId)!.Applications.Single(app => app.Name == appName).State;
        while (state != expected && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
            state = _registry.GetById(workspaceId)!.Applications.Single(app => app.Name == appName).State;
        }

        return state;
    }
}
