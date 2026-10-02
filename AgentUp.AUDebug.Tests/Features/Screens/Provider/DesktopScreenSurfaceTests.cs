using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
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

    private static FakeEnvironment Environment()
    {
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        environment.Executables["import"] = "/bin/import";
        return environment;
    }
}
