using System.Net;
using System.Text.Json;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Models;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Providers;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Services;
using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Shared.Providers;

namespace LocalInstaller.Smoke.Tests.Features.InstalledServiceValidation.Provider;

[TestFixture]
public sealed class CapabilityLifecycleSmokeTests
{
    [Test]
    public async Task RunAsync_startsCapabilityWorkspaceAndManagesIndividualAppLifecycle()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityLifecycleSmoke", $"{Guid.NewGuid():N}");
        var commands = new RecordingCommandRunner();
        using var http = new HttpClient(new SmokeHttpHandler());
        var assert = new FileAssertions();

        try
        {
            await new CapabilityLifecycleSmoke(commands, new CapabilityWorkspaceProvider(), new DotnetSmokeBuildProvider(commands), http).RunAsync(
                workDir,
                new InstalledServiceContext("agent-up", null, [], []),
                "http://localhost:5000",
                assert,
                CancellationToken.None);

            Assert.That(assert.Findings, Is.Empty);
            var config = await File.ReadAllTextAsync(Path.Join(workDir, "capability-workspace", "agent-up.json"));
            Assert.That(config, Does.Contain(ExpectedDockerImageForCurrentPlatform()));
            Assert.That(commands.Commands.Any(command => command.Arguments.Any(argument => argument.Contains("agent-up start", StringComparison.Ordinal))), Is.True);
            AssertWarmupBeforeStart(commands);
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
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityLifecycleSmoke", $"{Guid.NewGuid():N}");
        var commands = new RecordingCommandRunner();
        using var http = new HttpClient(new SmokeHttpHandler());
        var assert = new FileAssertions();

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE", "example/smoke:windows");
            await new CapabilityLifecycleSmoke(commands, new CapabilityWorkspaceProvider(), new DotnetSmokeBuildProvider(commands), http).RunAsync(
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

    [Test]
    public async Task RunAsync_waitsForTransientStoppingState()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityLifecycleSmoke", $"{Guid.NewGuid():N}");
        var commands = new RecordingCommandRunner();
        using var http = new HttpClient(new SmokeHttpHandler(transientDockerStopReads: 5));
        var assert = new FileAssertions();

        try
        {
            await new CapabilityLifecycleSmoke(commands, new CapabilityWorkspaceProvider(), new DotnetSmokeBuildProvider(commands), http).RunAsync(
                workDir,
                new InstalledServiceContext("agent-up", null, [], []),
                "http://localhost:5000",
                assert,
                CancellationToken.None);

            Assert.That(assert.Findings, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    [Test]
    public async Task RunAsync_acceptsStoppingAfterSuccessfulDockerStopRequest()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityLifecycleSmoke", $"{Guid.NewGuid():N}");
        var commands = new RecordingCommandRunner();
        using var http = new HttpClient(new SmokeHttpHandler(keepDockerStopping: true));
        var assert = new FileAssertions();

        try
        {
            await new CapabilityLifecycleSmoke(commands, new CapabilityWorkspaceProvider(), new DotnetSmokeBuildProvider(commands), http).RunAsync(
                workDir,
                new InstalledServiceContext("agent-up", null, [], []),
                "http://localhost:5000",
                assert,
                CancellationToken.None);

            Assert.That(assert.Findings, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    [Test]
    public async Task RunAsync_acceptsFailedDotnetStateAfterWorkspaceStop()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityLifecycleSmoke", $"{Guid.NewGuid():N}");
        var commands = new RecordingCommandRunner();
        using var http = new HttpClient(new SmokeHttpHandler(failDotnetAfterWorkspaceStop: true));
        var assert = new FileAssertions();

        try
        {
            await new CapabilityLifecycleSmoke(commands, new CapabilityWorkspaceProvider(), new DotnetSmokeBuildProvider(commands), http).RunAsync(
                workDir,
                new InstalledServiceContext("agent-up", null, [], []),
                "http://localhost:5000",
                assert,
                CancellationToken.None);

            Assert.That(assert.Findings, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    [Test]
    public async Task RunAsync_reportsPostStopPollFailureAsFinding()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityLifecycleSmoke", $"{Guid.NewGuid():N}");
        var commands = new RecordingCommandRunner();
        using var http = new HttpClient(new SmokeHttpHandler(invalidWorkspaceJsonAfterWorkspaceStop: true));
        var assert = new FileAssertions();

        try
        {
            await new CapabilityLifecycleSmoke(commands, new CapabilityWorkspaceProvider(), new DotnetSmokeBuildProvider(commands), http).RunAsync(
                workDir,
                new InstalledServiceContext("agent-up", null, [], []),
                "http://localhost:5000",
                assert,
                CancellationToken.None);

            Assert.That(assert.Findings, Has.Some.Matches<SmokeFinding>(finding =>
                finding.Code == "capability.smokedotnet.state" &&
                finding.Message.Contains("post-stop state poll failed", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    private static string ExpectedDockerImageForCurrentPlatform()
        => "nginx:alpine";

    private static int CommandIndex(RecordingCommandRunner commands, string fragment)
        => commands.Commands.FindIndex(command => command.Arguments.Any(argument => argument.Contains(fragment, StringComparison.Ordinal)));

    private static void AssertWarmupBeforeStart(RecordingCommandRunner commands)
    {
        var restoreIndex = CommandIndex(commands, "restore");
        var buildIndex = CommandIndex(commands, "build");
        var startIndex = CommandIndex(commands, "agent-up start");

        Assert.That(restoreIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(startIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(restoreIndex, Is.LessThan(buildIndex));
        Assert.That(buildIndex, Is.LessThan(startIndex));
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

    private sealed class SmokeHttpHandler(
        int transientDockerStopReads = 0,
        bool keepDockerStopping = false,
        bool failDotnetAfterWorkspaceStop = false,
        bool invalidWorkspaceJsonAfterWorkspaceStop = false) : HttpMessageHandler
    {
        private readonly List<HttpResponseMessage> _responses = [];
        private readonly Dictionary<string, string> _states = new(StringComparer.Ordinal)
        {
            ["SmokeDotnet"] = "Running",
            ["SmokeDocker"] = "Running"
        };
        private int _dockerStoppingReadsRemaining;
        private bool _workspaceStopped;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/workspaces")
                return JsonAsync(new[] { Workspace() });
            if (request.Method == HttpMethod.Get && path == "/api/workspaces/workspace-1")
            {
                if (invalidWorkspaceJsonAfterWorkspaceStop && _workspaceStopped)
                    return ResponseAsync(HttpStatusCode.OK, new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"));

                return JsonAsync(Workspace());
            }
            if (request.Method == HttpMethod.Post && path.EndsWith("/applications/SmokeDotnet/stop", StringComparison.Ordinal))
                return StateAsync("SmokeDotnet", "Stopped");
            if (request.Method == HttpMethod.Post && path.EndsWith("/applications/SmokeDotnet/start", StringComparison.Ordinal))
                return StateAsync("SmokeDotnet", "Running");
            if (request.Method == HttpMethod.Post && path.EndsWith("/applications/SmokeDocker/stop", StringComparison.Ordinal))
            {
                if (keepDockerStopping)
                {
                    _states["SmokeDocker"] = "Stopping";
                    return ResponseAsync(HttpStatusCode.NoContent);
                }

                if (transientDockerStopReads > 0)
                {
                    _states["SmokeDocker"] = "Stopping";
                    _dockerStoppingReadsRemaining = transientDockerStopReads;
                    return ResponseAsync(HttpStatusCode.NoContent);
                }

                return StateAsync("SmokeDocker", "Stopped");
            }
            if (request.Method == HttpMethod.Post && path == "/api/workspaces/workspace-1/stop")
            {
                _workspaceStopped = true;
                _states["SmokeDotnet"] = failDotnetAfterWorkspaceStop ? "Failed" : "Stopped";
                _states["SmokeDocker"] = keepDockerStopping ? "Stopping" : "Stopped";
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
        {
            var workspace = new
            {
                id = "workspace-1",
                displayName = "Capability Lifecycle Smoke Workspace",
                applications = new[]
                {
                    App("SmokeDotnet", "dotnet"),
                    App("SmokeDocker", "docker")
                }
            };

            if (_dockerStoppingReadsRemaining > 0)
            {
                _dockerStoppingReadsRemaining--;
                if (_dockerStoppingReadsRemaining == 0)
                    _states["SmokeDocker"] = "Stopped";
            }

            return workspace;
        }

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
