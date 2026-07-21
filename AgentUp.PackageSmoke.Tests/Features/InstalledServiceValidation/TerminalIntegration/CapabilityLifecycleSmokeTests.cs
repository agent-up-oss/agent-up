using System.Net;
using System.Text.Json;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.TerminalIntegration;

[TestFixture]
public sealed class CapabilityLifecycleSmokeTests
{
    [Test]
    public async Task RunAsync_startsCapabilityWorkspaceAndManagesIndividualAppLifecycle()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityLifecycleSmoke", Guid.NewGuid().ToString());
        var commands = new RecordingCommandRunner();
        using var http = new HttpClient(new SmokeHttpHandler());
        var assert = new FileAssertions();

        try
        {
            await new CapabilityLifecycleSmoke(commands, http).RunAsync(
                workDir,
                new InstalledServiceContext("agent-up", null, [], []),
                "http://localhost:5000",
                assert,
                CancellationToken.None);

            Assert.That(assert.Findings, Is.Empty);
            var config = await File.ReadAllTextAsync(Path.Join(workDir, "capability-workspace", "agent-up.json"));
            Assert.That(config, Does.Contain(ExpectedDockerImageForCurrentPlatform()));
            Assert.That(commands.Commands.Any(command => command.Arguments.Any(argument => argument.Contains("agent-up start", StringComparison.Ordinal))), Is.True);
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    [Test]
    public async Task RunAsync_usesDockerImageOverrideWhenProvided()
    {
        var previous = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE");
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityLifecycleSmoke", Guid.NewGuid().ToString());
        var commands = new RecordingCommandRunner();
        using var http = new HttpClient(new SmokeHttpHandler());
        var assert = new FileAssertions();

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE", "example/smoke:windows");
            await new CapabilityLifecycleSmoke(commands, http).RunAsync(
                workDir,
                new InstalledServiceContext("agent-up", null, [], []),
                "http://localhost:5000",
                assert,
                CancellationToken.None);

            Assert.That(assert.Findings, Is.Empty);
            var config = await File.ReadAllTextAsync(Path.Join(workDir, "capability-workspace", "agent-up.json"));
            Assert.That(config, Does.Contain("example/smoke:windows"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE", previous);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    private static string ExpectedDockerImageForCurrentPlatform()
    {
        if (!OperatingSystem.IsWindows())
            return "nginx:alpine";

        var tag = Environment.OSVersion.Version.Build >= 26000
            ? "windowsservercore-ltsc2025"
            : "windowsservercore-ltsc2022";
        return $"mcr.microsoft.com/windows/servercore/iis:{tag}";
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<CommandSpec> Commands { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(new CommandResult(
                0,
                command.Arguments.Any(argument => argument.Contains("agent-up start", StringComparison.Ordinal))
                    ? "Started workspace \"Capability Lifecycle Smoke Workspace\""
                    : "",
                ""));
        }
    }

    private sealed class SmokeHttpHandler : HttpMessageHandler
    {
        private readonly List<HttpResponseMessage> _responses = [];
        private readonly Dictionary<string, string> _states = new(StringComparer.Ordinal)
        {
            ["SmokeDotnet"] = "Running",
            ["SmokeDocker"] = "Running"
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/workspaces")
                return JsonAsync(new[] { Workspace() });
            if (request.Method == HttpMethod.Get && path == "/api/workspaces/workspace-1")
                return JsonAsync(Workspace());
            if (request.Method == HttpMethod.Post && path.EndsWith("/applications/SmokeDotnet/stop", StringComparison.Ordinal))
                return StateAsync("SmokeDotnet", "Stopped");
            if (request.Method == HttpMethod.Post && path.EndsWith("/applications/SmokeDotnet/start", StringComparison.Ordinal))
                return StateAsync("SmokeDotnet", "Running");
            if (request.Method == HttpMethod.Post && path.EndsWith("/applications/SmokeDocker/stop", StringComparison.Ordinal))
                return StateAsync("SmokeDocker", "Stopped");
            if (request.Method == HttpMethod.Post && path == "/api/workspaces/workspace-1/stop")
            {
                _states["SmokeDotnet"] = "Stopped";
                _states["SmokeDocker"] = "Stopped";
                return ResponseAsync(HttpStatusCode.NoContent);
            }

            return ResponseAsync(HttpStatusCode.NotFound);
        }

        private Task<HttpResponseMessage> StateAsync(string appName, string state)
        {
            _states[appName] = state;
            return ResponseAsync(HttpStatusCode.NoContent);
        }

        private Task<HttpResponseMessage> JsonAsync<T>(T value)
            => ResponseAsync(HttpStatusCode.OK, new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json"));

        private Task<HttpResponseMessage> ResponseAsync(HttpStatusCode statusCode, HttpContent? content = null)
            => Task.FromResult(Track(new HttpResponseMessage(statusCode) { Content = content }));

        private HttpResponseMessage Track(HttpResponseMessage response)
        {
            _responses.Add(response);
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var response in _responses)
                    response.Dispose();
                _responses.Clear();
            }

            base.Dispose(disposing);
        }

        private object Workspace()
            => new
            {
                id = "workspace-1",
                displayName = "Capability Lifecycle Smoke Workspace",
                applications = new[]
                {
                    App("SmokeDotnet", "dotnet"),
                    App("SmokeDocker", "docker")
                }
            };

        private object App(string name, string capabilityId)
            => new
            {
                name,
                capabilityId,
                state = _states[name],
                capabilityStatus = new { canRun = true }
            };
    }
}
