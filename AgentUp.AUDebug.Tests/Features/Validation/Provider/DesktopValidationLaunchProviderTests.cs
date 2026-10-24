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
        var provider = Provider(root, new FakeProcessRunner(), new FakeReadyProbe());

        var resolved = provider.ResolveOutputPath(null);

        Assert.That(resolved, Is.EqualTo(Path.Join(root, "artifacts", "desktop-validation")));
    }

    [Test]
    public async Task RunAsync_reusesAReadyServerAndLaunchesDesktopWithValidateEnv()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "run-desktop.sh"), "#!/bin/sh\n");
        var processes = new FakeProcessRunner();
        var provider = Provider(root, processes, new FakeReadyProbe());

        await provider.RunAsync(
            new DesktopValidationLaunchSpec("/tmp/out", "flow-1", "ws-1", "secret"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Started, Is.Empty);
            Assert.That(processes.Ran, Has.Count.EqualTo(1));
            Assert.That(processes.Ran[0].FileName, Is.EqualTo("bash"));
            Assert.That(processes.Ran[0].Environment!["AGENTUP_VALIDATE_SURFACE"], Is.EqualTo("desktop"));
            Assert.That(processes.Ran[0].Environment["AGENTUP_VALIDATE_OUT"], Is.EqualTo("/tmp/out"));
            Assert.That(processes.Ran[0].Environment["AGENTUP_VALIDATE_FLOW"], Is.EqualTo("flow-1"));
            Assert.That(processes.Ran[0].Environment["AGENTUP_VALIDATE_WORKSPACE"], Is.EqualTo("ws-1"));
            Assert.That(processes.Ran[0].Environment["AGENTUP_VALIDATE_SKIP_COSMETIC"], Is.EqualTo("1"));
            Assert.That(processes.Ran[0].Environment["AGENTUP_ADMIN_PASSWORD"], Is.EqualTo("secret"));
        });
    }

    [Test]
    public async Task RunAsync_startsTheServerWhenItIsNotReadyAndStopsItAfterwards()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-validate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, "AgentUp.Server"));
        File.WriteAllText(Path.Join(root, "run-desktop.sh"), "#!/bin/sh\n");
        var processes = new FakeProcessRunner();
        var probe = new FakeReadyProbe { Ready = { [DebugLayout.ServerUrl + DebugLayout.ServerReadyPath] = false } };
        var provider = Provider(root, processes, probe);

        await provider.RunAsync(
            new DesktopValidationLaunchSpec("/tmp/out", null, null, null),
            CancellationToken.None);

        Assert.That(processes.Started.Select(command => command.FileName), Does.Contain("dotnet"));
        Assert.That(processes.Killed, Is.Not.Empty);
    }

    private static DesktopValidationLaunchProvider Provider(
        string root,
        FakeProcessRunner processes,
        FakeReadyProbe probe)
        => new(processes, new FakePathValidator(root), new FakeEnvironment(), probe);
}
