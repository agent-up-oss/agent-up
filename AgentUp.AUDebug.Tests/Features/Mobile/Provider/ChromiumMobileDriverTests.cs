using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Mobile.Provider;

[TestFixture]
public sealed class ChromiumMobileDriverTests
{
    [Test]
    public void Login_whenCanceled_killsBrowser()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-mobile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumMobileDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.LoginAsync(DebugLayout.ServerUrl, "test", timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started, Has.Count.EqualTo(1));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
        Assert.That(processes.Started[0].FileName, Is.EqualTo("chromium"));
        Assert.That(processes.Started[0].Arguments, Does.Contain($"{DebugLayout.MobileUrl}/connect"));
    }

    [Test]
    public void Login_fallsBackToNixShellWhenChromiumIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-mobile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var driver = new ChromiumMobileDriver(processes, new FakeEnvironment(), new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.LoginAsync(DebugLayout.ServerUrl, "test", timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("nix-shell"));
        Assert.That(processes.Started[0].Arguments, Does.Contain("chromium"));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
    }
}
