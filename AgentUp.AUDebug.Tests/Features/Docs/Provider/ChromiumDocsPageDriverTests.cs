using AgentUp.AUDebug.Features.Docs.Providers;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Docs.Provider;

[TestFixture]
[NonParallelizable]
public sealed class ChromiumDocsPageDriverTests
{
    [Test]
    public void Capture_whenCanceled_killsBrowser()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumDocsPageDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}",
                Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png"),
                "What it is",
                true,
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started, Has.Count.EqualTo(1));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
        Assert.That(processes.Started[0].FileName, Is.EqualTo("chromium"));
        Assert.That(processes.Started[0].Arguments, Does.Contain($"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}"));
        Assert.That(processes.Started[0].Arguments, Does.Contain($"--remote-debugging-port={ChromiumDocsPageDriver.DebuggingPort}"));
        Assert.That(processes.Started[0].Arguments, Does.Contain($"--window-size={DebugLayout.DocsViewportWidth},{DebugLayout.DocsViewportHeight}"));
    }

    [Test]
    public void Capture_fallsBackToNixShellWhenChromiumIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var driver = new ChromiumDocsPageDriver(processes, new FakeEnvironment(), new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}/developer-guide/git",
                Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png"),
                null,
                false,
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("nix-shell"));
        Assert.That(processes.Started[0].Arguments, Does.Contain("chromium"));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
    }

    [Test]
    public void Capture_fallsBackToChromiumBrowserThenChrome()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium-browser"] = "/usr/bin/chromium-browser";
        var driver = new ChromiumDocsPageDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}/docs/",
                Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png"),
                null,
                false,
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("chromium-browser"));

        processes.Started.Clear();
        environment.Executables.Clear();
        environment.Executables["google-chrome"] = "/usr/bin/google-chrome";
        Assert.That(
            async () => await driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}/docs/",
                Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png"),
                null,
                false,
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("google-chrome"));
    }
}
