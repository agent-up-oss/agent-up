using System.Diagnostics;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Validation.DTOs;
using AgentUp.AUDebug.Features.Validation.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Validation.Provider;

[TestFixture]
public sealed class DesktopValidationLaunchProviderTests
{
    [Test]
    public void ResolveOutputPath_defaultsUnderTheRepository()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var provider = Provider(root, new FakeProcessRunner(), new FakeReadyProbe(), new FakeValidationWorkspaceFixture());

        var resolved = provider.ResolveOutputPath(null);

        Assert.That(resolved, Is.EqualTo(Path.Join(root, "artifacts", "desktop-validation")));
        Assert.That(provider.ResolveOutputPath("/tmp/out"), Is.EqualTo(Path.GetFullPath("/tmp/out")));
        Assert.That(provider.ResolveOutputPath("relative/out"), Is.EqualTo(Path.Join(root, "relative", "out")));
    }

    [Test]
    public async Task RunAsync_startsAnIsolatedServerRegistersTheWorkspaceAndLaunchesDesktop()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Desktop"));
        var processes = new FakeProcessRunner();
        var fixture = new FakeValidationWorkspaceFixture();
        var provider = Provider(root, processes, new FakeReadyProbe(), fixture);

        await provider.RunAsync(
            new DesktopValidationLaunchSpec("/tmp/out", "flow-1", "ws-1", "secret"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Started.Select(command => command.FileName), Does.Contain("dotnet"));
            Assert.That(processes.Started[0].Environment!["AGENTUP_AUTH_DISABLED"], Is.EqualTo("true"));
            Assert.That(processes.Started[0].Environment["Storage__DataDirectory"], Does.Contain("agent-up-desktop-validate").Or.Contain("fake-validate-data"));
            Assert.That(fixture.Prepared, Does.Contain(DebugLayout.ServerUrl));
            Assert.That(processes.Ran[0].FileName, Is.EqualTo("dotnet"));
            Assert.That(processes.Ran[0].Environment!["AGENTUP_VALIDATE_SURFACE"], Is.EqualTo("desktop"));
            Assert.That(processes.Ran[0].Environment["AGENTUP_VALIDATE_SERVER_LOG"], Is.EqualTo("/tmp/out/server.log"));
            Assert.That(processes.Killed, Is.Not.Empty);
        });
    }

    [Test]
    public async Task RunAsync_passesTheFixtureServerEnvironmentToTheIsolatedServer()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Desktop"));
        var processes = new FakeProcessRunner();
        var fixture = new FakeValidationWorkspaceFixture();
        fixture.Environment["Agents__test-agent-scripted__Command"] = "/tmp/test-agent-scripted";
        var provider = Provider(root, processes, new FakeReadyProbe(), fixture);

        await provider.RunAsync(
            new DesktopValidationLaunchSpec("/tmp/out", null, null, null),
            CancellationToken.None);

        Assert.That(processes.Started[0].Environment!["Agents__test-agent-scripted__Command"], Is.EqualTo("/tmp/test-agent-scripted"));
    }

    [Test]
    public async Task RunAsync_stopsTheIsolatedServerAfterwards()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Desktop"));
        var processes = new FakeProcessRunner();
        var provider = Provider(root, processes, new FakeReadyProbe(), new FakeValidationWorkspaceFixture());

        await provider.RunAsync(
            new DesktopValidationLaunchSpec("/tmp/out", null, null, null),
            CancellationToken.None);

        Assert.That(processes.Started.Select(command => command.FileName), Does.Contain("dotnet"));
        Assert.That(processes.Killed, Is.Not.Empty);
    }

    [Test]
    public async Task RunAsync_omitsBlankLaunchOverrides()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Desktop"));
        var processes = new FakeProcessRunner();
        var provider = Provider(root, processes, new FakeReadyProbe(), new FakeValidationWorkspaceFixture());

        await provider.RunAsync(
            new DesktopValidationLaunchSpec("/tmp/out", "  ", "\t", ""),
            CancellationToken.None);

        var environment = processes.Ran[0].Environment!;
        Assert.That(environment.ContainsKey(DesktopValidationLaunchKeys.Flow), Is.False);
        Assert.That(environment.ContainsKey(DesktopValidationLaunchKeys.Workspace), Is.False);
        Assert.That(environment.ContainsKey("AGENTUP_ADMIN_PASSWORD"), Is.False);
    }

    [Test]
    public async Task RunAsync_continuesWhenServerLogAttachIsRefused()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Desktop"));
        var processes = new FakeProcessRunner
        {
            StartOverride = _ => Process.Start(new ProcessStartInfo
            {
                FileName = "true",
                UseShellExecute = false
            })!
        };
        var provider = Provider(root, processes, new FakeReadyProbe(), new FakeValidationWorkspaceFixture());

        await provider.RunAsync(
            new DesktopValidationLaunchSpec("/tmp/out", null, null, null),
            CancellationToken.None);

        Assert.That(processes.Ran, Is.Not.Empty);
        Assert.That(processes.Killed, Is.Not.Empty);
    }

    [Test]
    public async Task RunAsync_appendsServerOutputToTheLog()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        var output = Path.Join(root, "out");
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Desktop"));
        Process? server = null;
        var processes = new FakeProcessRunner
        {
            StartOverride = _ =>
            {
                server = Process.Start(new ProcessStartInfo
                {
                    FileName = "sh",
                    Arguments = "-c 'echo hello-from-server; exec sleep 5'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                })!;
                return server;
            }
        };
        var provider = Provider(root, processes, new FakeReadyProbe(), new FakeValidationWorkspaceFixture());

        try
        {
            await provider.RunAsync(
                new DesktopValidationLaunchSpec(output, null, null, null),
                CancellationToken.None);

            var logPath = Path.Join(output, "server.log");
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline
                   && !(File.Exists(logPath) && File.ReadAllText(logPath).Contains("hello-from-server", StringComparison.Ordinal)))
            {
                await Task.Delay(50);
            }

            Assert.That(File.ReadAllText(logPath), Does.Contain("hello-from-server"));
        }
        finally
        {
            if (server is { HasExited: false })
                server.Kill(entireProcessTree: true);
            server?.Dispose();
        }
    }

    private static DesktopValidationLaunchProvider Provider(
        string root,
        FakeProcessRunner processes,
        FakeReadyProbe probe,
        FakeValidationWorkspaceFixture fixture)
        => new(processes, new FakePathValidator(root), new FakeEnvironment(), probe, fixture);
}
