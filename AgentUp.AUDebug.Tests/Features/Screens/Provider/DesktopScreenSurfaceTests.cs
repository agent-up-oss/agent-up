using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

[TestFixture]
public sealed class DesktopScreenSurfaceTests
{
    [Test]
    public async Task Open_pinsTheWindowToTheSizeTheRouteWasMeasuredAt()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var surface = new DesktopScreenSurface(processes, Environment(), new FakePathValidator("/tmp/au-debug-screens"));

        await surface.OpenAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                processes.Ran.Any(command =>
                    command.Arguments.Contains("windowsize")
                    && command.Arguments.Contains(DebugLayout.DesktopScreenshotWidth.ToString())
                    && command.Arguments.Contains(DebugLayout.DesktopScreenshotHeight.ToString())),
                Is.True);
            Assert.That(processes.Ran.Any(command => command.Arguments.Contains("windowmove")), Is.True);
        });
    }

    [Test]
    public async Task Run_clicksTypesAndSendsKeysThroughXdotool()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var surface = new DesktopScreenSurface(processes, Environment(), new FakePathValidator("/tmp/au-debug-screens"));

        await surface.RunAsync(
            [
                ScreenStepDto.Click(120, 240, 0),
                ScreenStepDto.Key("ctrl+a"),
                ScreenStepDto.Type("hello")
            ],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                processes.Ran.Any(command => command.Arguments.Contains("mousemove") && command.Arguments.Contains("240")),
                Is.True);
            Assert.That(processes.Ran.Any(command => command.Arguments.Contains("key") && command.Arguments.Contains("ctrl+a")), Is.True);
            Assert.That(processes.Ran.Any(command => command.Arguments.Contains("type") && command.Arguments.Contains("hello")), Is.True);
        });
    }

    [Test]
    public void Run_rejectsAStepThatBelongsToTheOtherSurface()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var surface = new DesktopScreenSurface(processes, Environment(), new FakePathValidator("/tmp/au-debug-screens"));

        Assert.That(
            async () => await surface.RunAsync([ScreenStepDto.Tap("Git")], CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("Tap"));
    }

    [Test]
    public void Capture_reportsAMissingWindowRatherThanAnEmptyFile()
    {
        var processes = new FakeProcessRunner { NextResult = new(1, "", "") };
        var surface = new DesktopScreenSurface(processes, Environment(), new FakePathValidator("/tmp/au-debug-screens"));

        Assert.That(
            async () => await surface.CaptureAsync("/tmp/au-debug-screens/git.png", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("Desktop window was not found"));
    }

    [Test]
    public async Task Capture_passesAHexX11WindowIdToImport()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var surface = new DesktopScreenSurface(processes, Environment(), new FakePathValidator("/tmp/au-debug-screens"));

        await surface.CaptureAsync("/tmp/au-debug-screens/git.png", CancellationToken.None);

        Assert.That(
            processes.Ran.Any(command =>
                command.FileName == "import"
                && command.Arguments.Contains("-window")
                && command.Arguments.Contains("0x1092")),
            Is.True);
    }

    [Test]
    public async Task Capture_keepsAnIdThatIsAlreadyHex()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "0x108a\n", "") };
        var surface = new DesktopScreenSurface(processes, Environment(), new FakePathValidator("/tmp/au-debug-screens"));

        await surface.CaptureAsync("/tmp/au-debug-screens/git.png", CancellationToken.None);

        Assert.That(
            processes.Ran.Any(command => command.FileName == "import" && command.Arguments.Contains("0x108a")),
            Is.True);
    }

    [Test]
    public async Task Capture_reportsWhenImportFailsAfterTheWindowWasFound()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        processes.OnRun = command =>
        {
            if (command.FileName == "import")
                processes.NextResult = new(1, "", "import: no pixmap");
        };
        var surface = new DesktopScreenSurface(processes, Environment(), new FakePathValidator("/tmp/au-debug-screens"));

        Assert.That(
            async () => await surface.CaptureAsync("/tmp/au-debug-screens/git.png", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("Desktop screen capture failed"));
    }

    [Test]
    public async Task Run_settlesWithoutCallingXdotoolAgain()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var surface = new DesktopScreenSurface(processes, Environment(), new FakePathValidator("/tmp/au-debug-screens"));
        await surface.OpenAsync(CancellationToken.None);
        var ran = processes.Ran.Count;

        await surface.RunAsync([ScreenStepDto.Settle(0)], CancellationToken.None);

        Assert.That(processes.Ran, Has.Count.EqualTo(ran));
    }

    [Test]
    public async Task Open_usesNixWhenXdotoolIsNotOnPath()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        var surface = new DesktopScreenSurface(processes, new FakeEnvironment(), new FakePathValidator("/tmp/au-debug-screens"));

        await surface.OpenAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Ran.All(command => command.FileName == "nix-shell"), Is.True);
            Assert.That(processes.Ran[0].Arguments, Does.Contain("xdotool"));
            Assert.That(surface.Surface, Is.EqualTo(ProductSurface.Desktop));
        });
    }

    [Test]
    public async Task Dispose_isANoOp()
    {
        var surface = new DesktopScreenSurface(new FakeProcessRunner(), Environment(), new FakePathValidator("/tmp/au-debug-screens"));

        await surface.DisposeAsync();

        Assert.That(surface.Surface, Is.EqualTo(ProductSurface.Desktop));
    }

    private static FakeEnvironment Environment()
    {
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        environment.Executables["import"] = "/bin/import";
        return environment;
    }
}
