using AgentUp.AUDebug.Features.Host.Providers;
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
        var supervisor = new HostProcessSupervisor(
            processes,
            new DebugPathValidator(root),
            new FakeEnvironment(),
            output);

        var session = await supervisor.StartAsync(CancellationToken.None);

        Assert.That(session.Processes, Has.Count.EqualTo(4));
        Assert.That(processes.Started.Select(command => command.FileName), Is.EquivalentTo(new[] { "dotnet", "bash", "npm", "npm" }));
        Assert.That(supervisor.HasLiveProcess(session), Is.True);
        await supervisor.StopAsync(session, CancellationToken.None);
        Assert.That(processes.Killed, Is.Not.Empty);
    }
}
