using AgentUp.AUDebug.Features.Desktop.Providers;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Desktop.Provider;

[TestFixture]
public sealed class XdoToolDesktopDriverTests
{
    [Test]
    public async Task Capture_usesImportOnceWindowExists()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        environment.Executables["import"] = "/bin/import";
        var paths = new FakePathValidator("/tmp/au-debug-desktop");
        Directory.CreateDirectory(paths.ScreenshotsDirectory);
        var driver = new XdoToolDesktopDriver(processes, environment, paths);

        await driver.CaptureAsync(Path.Join(paths.ScreenshotsDirectory, "desktop.png"), CancellationToken.None);

        Assert.That(processes.Ran.Any(command => command.FileName == "import"), Is.True);
        Assert.That(
            processes.Ran.Any(command =>
                command.FileName == "import"
                && command.Arguments.Any(argument => argument.StartsWith("0x", StringComparison.Ordinal))),
            Is.True);
        Assert.That(processes.Ran.Any(command => command.Arguments.Contains("--onlyvisible")), Is.True);
        Assert.That(processes.Ran.Any(command => command.Arguments.Contains("--class")), Is.True);
        Assert.That(processes.Ran.Any(command => command.Arguments.Contains(DebugLayout.DesktopWindowClass)), Is.True);
        Assert.That(processes.Ran.All(command => command.Environment!["DISPLAY"] == ":0"), Is.True);
    }

    [Test]
    public async Task Login_sendsPasswordOnStdin()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        var driver = new XdoToolDesktopDriver(processes, environment, new FakePathValidator("/tmp/au-debug-desktop"));

        await driver.LoginAsync("secret", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Ran.Any(command => command.StandardInput == "secret"), Is.True);
            Assert.That(
                processes.Ran.Any(command =>
                    command.Arguments.Contains("mousemove")
                    && command.Arguments.Contains(DebugLayout.DesktopLoginFieldX.ToString())
                    && command.Arguments.Contains(DebugLayout.DesktopLoginButtonY.ToString())),
                Is.True);
            Assert.That(processes.Ran.Any(command => command.Arguments.Contains("click")), Is.True);
        });
    }

    [Test]
    public async Task HasWindow_isFalseWhenSearchFails()
    {
        var processes = new FakeProcessRunner { NextResult = new(1, "", "missing") };
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        var driver = new XdoToolDesktopDriver(processes, environment, new FakePathValidator("/tmp/au-debug-desktop"));

        Assert.That(await driver.HasWindowAsync(CancellationToken.None), Is.False);
    }

    [Test]
    public async Task MissingWindow_throws()
    {
        var processes = new FakeProcessRunner { NextResult = new(1, "", "missing") };
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        var driver = new XdoToolDesktopDriver(processes, environment, new FakePathValidator("/tmp/au-debug-desktop"));

        Assert.That(
            async () => await driver.CaptureAsync("/tmp/au-debug-desktop/.git/agent-up/au-debug/screenshots/x.png", CancellationToken.None),
            Throws.InvalidOperationException);
    }

    [Test]
    public async Task Capture_keepsHexWindowIds()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "0xabc\n", "") };
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        environment.Executables["import"] = "/bin/import";
        var paths = new FakePathValidator("/tmp/au-debug-desktop");
        Directory.CreateDirectory(paths.ScreenshotsDirectory);
        var driver = new XdoToolDesktopDriver(processes, environment, paths);

        await driver.CaptureAsync(Path.Join(paths.ScreenshotsDirectory, "desktop.png"), CancellationToken.None);

        Assert.That(
            processes.Ran.Any(command => command.FileName == "import" && command.Arguments.Contains("0xabc")),
            Is.True);
    }

    [Test]
    public async Task Capture_reportsImportFailure()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        processes.OnRun = command =>
        {
            if (command.FileName == "import")
                processes.NextResult = new(1, "", "import boom");
        };
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        environment.Executables["import"] = "/bin/import";
        var paths = new FakePathValidator("/tmp/au-debug-desktop");
        Directory.CreateDirectory(paths.ScreenshotsDirectory);
        var driver = new XdoToolDesktopDriver(processes, environment, paths);

        Assert.That(
            async () => await driver.CaptureAsync(Path.Join(paths.ScreenshotsDirectory, "desktop.png"), CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("import boom"));
    }

    [Test]
    public async Task HasWindow_fallsBackToNixShellWhenToolsAreMissing()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var driver = new XdoToolDesktopDriver(processes, new FakeEnvironment(), new FakePathValidator("/tmp/au-debug-desktop"));

        Assert.That(await driver.HasWindowAsync(CancellationToken.None), Is.True);
        Assert.That(processes.Ran[0].FileName, Is.EqualTo("nix-shell"));
        Assert.That(processes.Ran[0].Arguments, Does.Contain("xdotool"));
    }

    [Test]
    public void WaitForWindow_cancelsWhileSearching()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "", "") };
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        var driver = new XdoToolDesktopDriver(processes, environment, new FakePathValidator("/tmp/au-debug-desktop"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));

        Assert.That(
            async () => await driver.WaitForWindowAsync(timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
