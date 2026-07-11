using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.CLI.Http;
using AgentUp.CLI.Tests.Fake;
using AgentUp.Server.Features.Workspaces.Controllers;
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

        _workspaceDir = Path.Combine(Path.GetTempPath(), "AgentUp-E2E", Guid.NewGuid().ToString());
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
        if (Directory.Exists(_workspaceDir))
            Directory.Delete(_workspaceDir, recursive: true);
    }

    [Test]
    public async Task Register_ExitsZero_AndPrintsWorkspaceName()
    {
        var output = new StringWriter();
        var runner = new CliRunner($"http://localhost:{_port}", _workspaceDir, output);

        var exitCode = await runner.RunAsync(["register"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Test Project"));
    }

    [Test]
    public async Task Register_UsesAgentUpJsonName_AsDisplayName()
    {
        await new CliRunner($"http://localhost:{_port}", _workspaceDir).RunAsync(["register"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(workspaces, Has.Count.EqualTo(1));
        Assert.That(workspaces![0].DisplayName, Is.EqualTo("Test Project"));
    }

    [Test]
    public async Task Register_PopulatesGitBranchAndCommit()
    {
        await new CliRunner($"http://localhost:{_port}", _workspaceDir).RunAsync(["register"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var w = workspaces![0];
        Assert.That(w.Branch, Is.Not.Empty);
        Assert.That(w.Commit, Has.Length.EqualTo(40));
    }

    [Test]
    public async Task Register_SetsWorktreePath_ToCurrentDirectory()
    {
        await new CliRunner($"http://localhost:{_port}", _workspaceDir).RunAsync(["register"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(workspaces![0].WorktreePath, Is.EqualTo(_workspaceDir));
    }

    [Test]
    public async Task Register_Twice_DoesNotDuplicate()
    {
        await new CliRunner($"http://localhost:{_port}", _workspaceDir).RunAsync(["register"]);
        await new CliRunner($"http://localhost:{_port}", _workspaceDir).RunAsync(["register"]);

        var workspaces = await _serverClient.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(workspaces, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Register_Fails_WhenAgentUpJsonMissing()
    {
        File.Delete(Path.Combine(_workspaceDir, "agent-up.json"));
        var output = new StringWriter();

        var exitCode = await new CliRunner($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["register"]);

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Error"));
    }

    [Test]
    public async Task Register_Fails_WhenServerUnreachable()
    {
        var output = new StringWriter();

        var exitCode = await new CliRunner("http://localhost:1", _workspaceDir, output).RunAsync(["register"]);

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Error"));
    }

    [Test]
    public async Task List_ShowsEmpty_WhenNoWorkspacesRegistered()
    {
        var output = new StringWriter();

        var exitCode = await new CliRunner($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["list"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("No workspaces"));
    }

    [Test]
    public async Task List_ShowsRegisteredWorkspaces()
    {
        await new CliRunner($"http://localhost:{_port}", _workspaceDir).RunAsync(["register"]);

        var output = new StringWriter();
        var exitCode = await new CliRunner($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["list"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Test Project"));
    }

    [Test]
    public async Task List_ShowsMultipleWorkspaces()
    {
        var secondDir = Path.Combine(Path.GetTempPath(), "AgentUp-E2E", Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(secondDir);
            await InitGitRepoAsync(secondDir);
            await WriteAgentUpJsonAsync(secondDir, "Second Project");

            await new CliRunner($"http://localhost:{_port}", _workspaceDir).RunAsync(["register"]);
            await new CliRunner($"http://localhost:{_port}", secondDir).RunAsync(["register"]);

            var output = new StringWriter();
            await new CliRunner($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["list"]);

            var text = output.ToString();
            Assert.That(text, Does.Contain("Test Project"));
            Assert.That(text, Does.Contain("Second Project"));
        }
        finally
        {
            if (Directory.Exists(secondDir))
                Directory.Delete(secondDir, recursive: true);
        }
    }

    [Test]
    public async Task Status_ShowsCurrentWorkspace_AfterRegister()
    {
        await new CliRunner($"http://localhost:{_port}", _workspaceDir).RunAsync(["register"]);

        var output = new StringWriter();
        var exitCode = await new CliRunner($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["status"]);

        Assert.That(exitCode, Is.EqualTo(0));
        var text = output.ToString();
        Assert.That(text, Does.Contain("Test Project"));
        Assert.That(text, Does.Contain(_workspaceDir));
        Assert.That(text, Does.Contain("Stopped"));
    }

    [Test]
    public async Task Status_ReturnsNonZero_WhenNotRegistered()
    {
        var output = new StringWriter();
        var exitCode = await new CliRunner($"http://localhost:{_port}", _workspaceDir, output).RunAsync(["status"]);

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("No workspace registered"));
    }

    [Test]
    public async Task Help_ExitsZero_AndListsCommands()
    {
        var output = new StringWriter();
        var exitCode = await new CliRunner($"http://localhost:{_port}", _workspaceDir, output).RunAsync([]);

        Assert.That(exitCode, Is.EqualTo(0));
        var text = output.ToString();
        Assert.That(text, Does.Contain("register"));
        Assert.That(text, Does.Contain("list"));
        Assert.That(text, Does.Contain("status"));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static WebApplication BuildServer(int port)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [$"--urls=http://localhost:{port}"]
        });
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddSingleton<IWorkspaceRegistry>(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        app.MapWorkspaces();
        return app;
    }

    private static async Task InitGitRepoAsync(string dir)
    {
        await RunProcessAsync("git", "init", dir);
        await RunProcessAsync("git", "config user.email test@example.com", dir);
        await RunProcessAsync("git", "config user.name Test", dir);
        await File.WriteAllTextAsync(Path.Combine(dir, ".gitkeep"), "");
        await RunProcessAsync("git", "add .", dir);
        await RunProcessAsync("git", "commit -m init", dir);
    }

    private static Task WriteAgentUpJsonAsync(string dir, string name) =>
        File.WriteAllTextAsync(Path.Combine(dir, "agent-up.json"),
            $$"""{"name":"{{name}}"}""");

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

    private static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
