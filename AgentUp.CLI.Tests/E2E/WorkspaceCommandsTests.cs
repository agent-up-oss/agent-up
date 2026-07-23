using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.CLI.Composition;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Tests.Fake;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Services;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentUp.CLI.Tests.E2E;

[TestFixture]
public class WorkspaceCommandsTests
{
    private WebApplication _server = null!;
    private int _port;
    private string _workspaceDir = null!;
    private HttpClient _serverClient = null!;

    [SetUp]
    public async Task SetUp()
    {
        _port = FindFreePort();
        _server = BuildServer(_port);
        await _server.StartAsync();
        _serverClient = new HttpClient { BaseAddress = new Uri($"http://localhost:{_port}") };

        _workspaceDir = Path.Join(Path.GetTempPath(), "AgentUp-E2E", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_workspaceDir);
        await InitGitRepoAsync(_workspaceDir);
        await WriteAgentUpJsonAsync(_workspaceDir, "Test Project");
    }

    [TearDown]
    public async Task TearDown()
    {
        _serverClient.Dispose();
        await _server.StopAsync();
        await _server.DisposeAsync();
        DeleteDirectoryIfExists(_workspaceDir);
    }

    // ── start ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Start_ExitsZero_AndPrintsWorkspaceName()
    {
        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["start"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Test Project"));
    }

    [Test]
    public async Task Start_UsesAgentUpJsonName_AsDisplayName()
    {
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(workspaces, Has.Count.EqualTo(1));
        Assert.That(workspaces![0].DisplayName, Is.EqualTo("Test Project"));
    }

    [Test]
    public async Task Start_PopulatesGitBranchAndCommit()
    {
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var w = workspaces![0];
        Assert.That(w.Branch, Is.Not.Empty);
        Assert.That(w.Commit, Has.Length.EqualTo(40));
    }

    [Test]
    public async Task Start_RegistersWorkspace_WhenDirectoryIsNotGitRepository()
    {
        var plainDir = Path.Join(Path.GetTempPath(), "AgentUp-E2E", Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(plainDir);
            await WriteAgentUpJsonAsync(plainDir, "Plain Project");
            using var output = new StringWriter();

            var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", plainDir, output).RunAsync(["start"]);

            Assert.That(exitCode, Is.EqualTo(0));
            var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var workspace = workspaces!.Single(w => w.DisplayName == "Plain Project");
            Assert.That(workspace.RepositoryPath, Is.EqualTo(plainDir));
            Assert.That(workspace.WorktreePath, Is.EqualTo(plainDir));
            Assert.That(workspace.Branch, Is.EqualTo("not on a git branch"));
            Assert.That(workspace.Commit, Is.Empty);
            Assert.That(output.ToString(), Does.Contain("not on a git branch"));
        }
        finally
        {
            if (Directory.Exists(plainDir))
                Directory.Delete(plainDir, recursive: true);
        }
    }

    [Test]
    public async Task Start_SetsWorktreePath_ToCurrentDirectory()
    {
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(workspaces![0].WorktreePath, Is.EqualTo(_workspaceDir));
    }

    [Test]
    public async Task Start_Twice_DoesNotDuplicate()
    {
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(workspaces, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Start_PushesApplicationDefinitions_ToServer()
    {
        await WriteAgentUpJsonAsync(_workspaceDir, "My App",
        [
            new
            {
                name = "Frontend",
                command = "npm run dev",
                path = "./ui",
                environment = new Dictionary<string, string> { ["PUBLIC_MODE"] = "dev" },
                environmentFiles = new[] { ".env" }
            },
            new { name = "Backend", command = "dotnet run", path = "./api" }
        ]);

        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(workspaces![0].Applications, Has.Count.EqualTo(2));
        Assert.That(workspaces[0].Applications[0].Name, Is.EqualTo("Frontend"));
        Assert.That(workspaces[0].Applications[0].Environment!["PUBLIC_MODE"], Is.EqualTo("dev"));
        Assert.That(workspaces[0].Applications[0].EnvironmentFiles, Is.EqualTo(new[] { ".env" }));
        Assert.That(workspaces[0].Applications[1].Name, Is.EqualTo("Backend"));
    }

    [Test]
    public async Task Start_ListsApplications_InOutput()
    {
        await WriteAgentUpJsonAsync(_workspaceDir, "My App",
        [
            new { name = "Frontend", command = "npm run dev", path = (string?)null }
        ]);

        using var output = new StringWriter();
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["start"]);

        var text = output.ToString();
        Assert.That(text, Does.Contain("Frontend"));
        Assert.That(text, Does.Contain("npm run dev"));
    }

    [Test]
    public async Task Start_ApplicationsListedViaApi()
    {
        await WriteAgentUpJsonAsync(_workspaceDir, "My App",
        [
            new { name = "Docs", command = "npm run start", path = "docs" }
        ]);

        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var workspaceId = workspaces![0].Id;

        var appsJson = await _serverClient.GetFromJsonAsync<List<JsonElement>>(
            $"/api/workspaces/{workspaceId}/applications",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(appsJson, Has.Count.EqualTo(1));
        Assert.That(appsJson![0].GetProperty("name").GetString(), Is.EqualTo("Docs"));
        Assert.That(appsJson[0].GetProperty("command").GetString(), Is.EqualTo("npm run start"));
    }

    [Test]
    public async Task Start_Fails_WhenAgentUpJsonMissing()
    {
        File.Delete(Path.Join(_workspaceDir, "agent-up.json"));
        using var output = new StringWriter();

        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["start"]);

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Error"));
    }

    [Test]
    public async Task Start_Fails_WhenServerUnreachable()
    {
        using var output = new StringWriter();

        var exitCode = await CliRunnerFactory.Create("http://localhost:1", _workspaceDir, output).RunAsync(["start"]);

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Error"));
    }

    // ── stop ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Stop_ExitsZero_AndPrintsWorkspaceName()
    {
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["stop"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Test Project"));
    }

    [Test]
    public async Task Stop_SetsWorkspaceState_ToStopped()
    {
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["stop"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(workspaces![0].State, Is.EqualTo("Stopped"));
    }

    [Test]
    public async Task Stop_Fails_WhenWorkspaceNotStarted()
    {
        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["stop"]);

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Error"));
    }

    // ── list ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task List_ShowsEmpty_WhenNoWorkspacesRegistered()
    {
        using var output = new StringWriter();

        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["list"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("No workspaces"));
    }

    [Test]
    public async Task List_ShowsRegisteredWorkspaces()
    {
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["list"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Test Project"));
    }

    [Test]
    public async Task List_ShowsMultipleWorkspaces()
    {
        var secondDir = Path.Join(Path.GetTempPath(), "AgentUp-E2E", Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(secondDir);
            await InitGitRepoAsync(secondDir);
            await WriteAgentUpJsonAsync(secondDir, "Second Project");

            await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);
            await CliRunnerFactory.Create($"http://localhost:{_port}", secondDir).RunAsync(["start"]);

            using var output = new StringWriter();
            await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["list"]);

            var text = output.ToString();
            Assert.That(text, Does.Contain("Test Project"));
            Assert.That(text, Does.Contain("Second Project"));
        }
        finally
        {
            DeleteDirectoryIfExists(secondDir);
        }
    }

    // ── status ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Status_ShowsCurrentWorkspace_AfterStart()
    {
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["status"]);

        Assert.That(exitCode, Is.EqualTo(0));
        var text = output.ToString();
        Assert.That(text, Does.Contain("Test Project"));
        Assert.That(text, Does.Contain(_workspaceDir));
        Assert.That(text, Does.Contain("Running"));
    }

    [Test]
    public async Task Status_ReturnsNonZero_WhenNotStarted()
    {
        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["status"]);

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("No workspace registered"));
    }

    // ── help ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Help_ExitsZero_AndListsCommands()
    {
        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync([]);

        Assert.That(exitCode, Is.EqualTo(0));
        var text = output.ToString();
        Assert.That(text, Does.Contain("start"));
        Assert.That(text, Does.Contain("list"));
        Assert.That(text, Does.Contain("status"));
        Assert.That(text, Does.Not.Contain("register"));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static WebApplication BuildServer(int port)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [$"--urls=http://localhost:{port}"]
        });
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(WorkspacesController).Assembly)
            .AddJsonOptions(opts =>
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IOutputRepository, InMemoryOutputRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton(_ => new CapabilityReconciliationService([]));
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<IWorkspaceProcessManager, NullWorkspaceProcessManager>();
        builder.Services.AddSingleton<ProcessOutputService>();
        builder.Services.AddSingleton<ProcessesController>();
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<WorkspaceStateController>();
        builder.Services.AddSingleton<WorkspaceLifecycleService>();
        builder.Services.AddSingleton<ApplicationLifecycleService>();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        app.MapControllers();
        return app;
    }

    private static async Task InitGitRepoAsync(string dir)
    {
        await RunProcessAsync("git", "init", dir);
        await RunProcessAsync("git", "config user.email test@example.com", dir);
        await RunProcessAsync("git", "config user.name Test", dir);
        await File.WriteAllTextAsync(Path.Join(dir, ".gitkeep"), "");
        await RunProcessAsync("git", "add .", dir);
        await RunProcessAsync("git", "commit -m init", dir);
    }

    [Test]
    public async Task Start_PushesDockerServices_ToServer()
    {
        await WriteAgentUpJsonAsync(_workspaceDir, "My App",
            applications: [],
            services: [new { name = "Database", image = "postgres:16", ports = new[] { new { variable = "DB_PORT", defaultPort = 5432 } } }]);

        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir).RunAsync(["start"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(workspaces![0].Applications, Has.Count.EqualTo(1));
        Assert.That(workspaces[0].Applications[0].Name, Is.EqualTo("Database"));
    }

    [Test]
    public async Task Start_ListsDockerServices_InOutput()
    {
        await WriteAgentUpJsonAsync(_workspaceDir, "My App",
            applications: [],
            services: [new { name = "Database", image = "postgres:16" }]);

        using var output = new StringWriter();
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["start"]);

        var text = output.ToString();
        Assert.That(text, Does.Contain("Database"));
        Assert.That(text, Does.Contain("postgres:16"));
    }

    [Test]
    public async Task Start_PushesTypedDotnetAndDockerCapabilities_ToServer()
    {
        var json = JsonSerializer.Serialize(new
        {
            name = "Typed App",
            dotnet = new[]
            {
                new
                {
                    name = "Api",
                    sdk = "10.0.x",
                    run = new { project = "src/Api/Api.csproj", arguments = new[] { "--no-launch-profile" } },
                    environment = new Dictionary<string, string> { ["ASPNETCORE_ENVIRONMENT"] = "Development" },
                    environmentFiles = new[] { ".env" },
                    ports = new[] { new { variable = "API_PORT", defaultPort = 5000 } }
                }
            },
            docker = new[]
            {
                new
                {
                    name = "Database",
                    image = "postgres:17",
                    environment = new Dictionary<string, string> { ["POSTGRES_USER"] = "user" },
                    environmentFiles = new[] { ".env.database" },
                    ports = new[] { new { variable = "DB_PORT", defaultPort = 5432 } }
                }
            }
        });
        await File.WriteAllTextAsync(Path.Join(_workspaceDir, "agent-up.json"), json);

        using var output = new StringWriter();
        await CliRunnerFactory.Create($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["start"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(workspaces![0].Applications.Select(app => app.Name), Is.EquivalentTo(new[] { "Api", "Database" }));
        Assert.That(workspaces[0].Applications.Single(app => app.Name == "Api").Environment!["ASPNETCORE_ENVIRONMENT"], Is.EqualTo("Development"));
        Assert.That(workspaces[0].Applications.Single(app => app.Name == "Api").EnvironmentFiles, Is.EqualTo(new[] { ".env" }));
        Assert.That(workspaces[0].Applications.Single(app => app.Name == "Database").Environment!["POSTGRES_USER"], Is.EqualTo("user"));
        Assert.That(workspaces[0].Applications.Single(app => app.Name == "Database").EnvironmentFiles, Is.EqualTo(new[] { ".env.database" }));
        Assert.That(output.ToString(), Does.Contain(".NET (1):"));
        Assert.That(output.ToString(), Does.Contain("Docker (1):"));
    }

    private static Task WriteAgentUpJsonAsync(string dir, string name) =>
        File.WriteAllTextAsync(Path.Join(dir, "agent-up.json"),
            $$"""{"name":"{{name}}"}""");

    private static Task WriteAgentUpJsonAsync(string dir, string name, IEnumerable<object> applications)
    {
        var json = JsonSerializer.Serialize(new { name, applications });
        return File.WriteAllTextAsync(Path.Join(dir, "agent-up.json"), json);
    }

    private static Task WriteAgentUpJsonAsync(string dir, string name, IEnumerable<object> applications, IEnumerable<object> services)
    {
        var json = JsonSerializer.Serialize(new { name, applications, services });
        return File.WriteAllTextAsync(Path.Join(dir, "agent-up.json"), json);
    }

    private static async Task RunProcessAsync(string executable, string arguments, string workingDir)
    {
        using var p = Process.Start(new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException($"Failed to start {executable}.");

        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            var err = await p.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"{executable} {arguments} failed: {err}");
        }
    }

    private static void DeleteDirectoryIfExists(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                ClearReadOnlyAttributes(directory);
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
        }

        ClearReadOnlyAttributes(directory);
        Directory.Delete(directory, recursive: true);
    }

    private static void ClearReadOnlyAttributes(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
            File.SetAttributes(path, FileAttributes.Normal);

        File.SetAttributes(directory, FileAttributes.Normal);
    }

    private static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
