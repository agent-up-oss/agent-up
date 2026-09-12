using AgentUp.AUDebug.Shared.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Host.Provider;

[TestFixture]
public sealed class ChromiumScreenshotDriverTests
{
    [Test]
    public async Task Capture_usesChromiumOnPath()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-shot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git", "agent-up", "au-debug", "screenshots"));
        var destination = Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png");
        var processes = new FakeProcessRunner();
        processes.OnRun = _ => File.WriteAllBytes(destination, [1, 2, 3]);
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumScreenshotDriver(processes, environment, new DebugPathValidator(root));

        await driver.CaptureAsync("http://127.0.0.1:10100/design-system", destination, CancellationToken.None);

        Assert.That(processes.Ran[0].FileName, Is.EqualTo("chromium"));
        Assert.That(processes.Ran[0].Arguments, Does.Contain($"--screenshot={destination}"));
        Assert.That(processes.Ran[0].Arguments, Does.Contain("--virtual-time-budget=8000"));
        Assert.That(processes.Ran[0].Arguments, Does.Not.Contain("--user-data-dir="));
    }

    [Test]
    public async Task Capture_reusesUserDataDirectory()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-shot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git", "agent-up", "au-debug", "screenshots"));
        var destination = Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "mobile.png");
        var profile = Path.Join(root, ".git", "agent-up", "au-debug", "chrome-mobile");
        var processes = new FakeProcessRunner();
        processes.OnRun = _ => File.WriteAllBytes(destination, [1]);
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumScreenshotDriver(processes, environment, new DebugPathValidator(root));

        await driver.CaptureAsync("http://127.0.0.1:10102/", destination, CancellationToken.None, profile);

        Assert.That(processes.Ran[0].Arguments, Does.Contain($"--user-data-dir={Path.GetFullPath(profile)}"));
    }

    [Test]
    public async Task Capture_fallsBackToNixShell()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-shot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git", "agent-up", "au-debug", "screenshots"));
        var destination = Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png");
        var processes = new FakeProcessRunner();
        processes.OnRun = _ => File.WriteAllBytes(destination, [1]);
        var driver = new ChromiumScreenshotDriver(processes, new FakeEnvironment(), new DebugPathValidator(root));

        await driver.CaptureAsync("http://example.invalid/", destination, CancellationToken.None);

        Assert.That(processes.Ran[0].FileName, Is.EqualTo("nix-shell"));
        Assert.That(processes.Ran[0].Arguments, Does.Contain("chromium"));
    }
}
