using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Features.Host.Providers;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Host.Provider;

[TestFixture]
public sealed class HostProcessSupervisorTests
{
    [Test]
    public async Task Start_launchesFourProcesses()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-host", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Mobile"));
        Directory.CreateDirectory(Path.Join(root, "docs"));
        File.WriteAllText(Path.Join(root, "run-desktop.sh"), "#!/bin/sh\n");
        using var output = new StringWriter();
        var processes = new FakeProcessRunner();
        var supervisor = Supervisor(processes, root, output, BusyServer());

        var session = await supervisor.StartAsync(CancellationToken.None);

        Assert.That(session.Processes, Has.Count.EqualTo(4));
        Assert.That(processes.Started.Select(command => command.FileName), Is.EquivalentTo(new[] { "dotnet", "bash", "npm", "npm" }));
        Assert.That(supervisor.HasLiveProcess(session), Is.True);
        await supervisor.StopAsync(session, CancellationToken.None);
        Assert.That(processes.Killed, Is.Not.Empty);
    }

    [Test]
    public async Task Start_reusesReadyServer()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-host", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Mobile"));
        Directory.CreateDirectory(Path.Join(root, "docs"));
        File.WriteAllText(Path.Join(root, "run-desktop.sh"), "#!/bin/sh\n");
        var processes = new FakeProcessRunner();
        var supervisor = Supervisor(processes, root, TextWriter.Null, new FakeReadyProbe());

        var session = await supervisor.StartAsync(CancellationToken.None);

        Assert.That(session.Processes[0].Pid, Is.EqualTo(DebugLayout.ReusedProcessPid));
        Assert.That(processes.Started.Select(command => command.FileName), Is.EquivalentTo(new[] { "bash", "npm", "npm" }));
        await supervisor.StopAsync(session, CancellationToken.None);
        Assert.That(processes.Killed, Does.Not.Contain(DebugLayout.ReusedProcessPid));
    }

    [Test]
    public async Task WaitAsync_cancel_writesStopping()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-host", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var output = new StringWriter();
        var supervisor = Supervisor(new FakeProcessRunner(), root, output, BusyServer());
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        await supervisor.WaitAsync(timeout.Token);

        Assert.That(output.ToString(), Does.Contain("au-debug: stopping"));
    }

    [Test]
    public async Task Stop_killsForeignSupervisorPid()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-host", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var processes = new FakeProcessRunner();
        var supervisor = Supervisor(processes, root, TextWriter.Null, BusyServer());
        var session = new HostSessionDto(
            999999,
            root,
            Path.Join(root, ".git", "agent-up", "au-debug"),
            [new HostedProcessDto("server", 11, Path.Join(root, ".git", "agent-up", "au-debug", "logs", "server.log"), DebugLayout.ServerUrl)]);

        await supervisor.StopAsync(session, CancellationToken.None);

        Assert.That(processes.Killed, Does.Contain(11));
        Assert.That(processes.Killed, Does.Contain(999999));
    }

    [Test]
    public async Task Stop_skipsReusedServerPid()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-host", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var processes = new FakeProcessRunner();
        var supervisor = Supervisor(processes, root, TextWriter.Null, BusyServer());
        var session = new HostSessionDto(
            Environment.ProcessId,
            root,
            Path.Join(root, ".git", "agent-up", "au-debug"),
            [
                new HostedProcessDto("server", DebugLayout.ReusedProcessPid, Path.Join(root, ".git", "agent-up", "au-debug", "logs", "server.log"), DebugLayout.ServerUrl),
                new HostedProcessDto("desktop", 12, Path.Join(root, ".git", "agent-up", "au-debug", "logs", "desktop.log"), null)
            ]);

        await supervisor.StopAsync(session, CancellationToken.None);

        Assert.That(processes.Killed, Is.EqualTo(new[] { 12 }));
    }

    private static HostProcessSupervisor Supervisor(
        IAllowlistedProcessRunner processes,
        string root,
        TextWriter output,
        IHostReadyProbe probe)
        => new(processes, new DebugPathValidator(root), new FakeEnvironment(), probe, output);

    private static FakeReadyProbe BusyServer()
    {
        var probe = new FakeReadyProbe();
        probe.Ready[DebugLayout.ServerUrl + DebugLayout.ServerReadyPath] = false;
        return probe;
    }
}
