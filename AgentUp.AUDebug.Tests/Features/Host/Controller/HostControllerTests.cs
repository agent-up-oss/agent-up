using AgentUp.AUDebug.Features.Desktop.Controllers;
using AgentUp.AUDebug.Features.Desktop.Services;
using AgentUp.AUDebug.Features.Docs.Controllers;
using AgentUp.AUDebug.Features.Docs.Services;
using AgentUp.AUDebug.Features.Host.Controllers;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Providers;
using AgentUp.AUDebug.Features.Host.Services;
using AgentUp.AUDebug.Features.Mobile.Controllers;
using AgentUp.AUDebug.Features.Mobile.Services;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Host.Controller;

[TestFixture]
public sealed class HostControllerTests
{
    [Test]
    public async Task Help_printsUsage()
    {
        using var output = new StringWriter();
        var exit = await Controller(output).RunAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.EqualTo(0));
            Assert.That(output.ToString(), Does.Contain("Usage: au-debug"));
            Assert.That(output.ToString(), Does.Contain("desktop screenshot"));
        });
    }

    [Test]
    public async Task UnknownFlag_returnsError()
    {
        using var output = new StringWriter();
        var exit = await Controller(output).RunAsync(["--nope"]);

        Assert.That(exit, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("unknown argument"));
    }

    [Test]
    public async Task UpDetach_printsReady()
    {
        using var output = new StringWriter();
        var exit = await Controller(output).RunAsync(["up", "--detach"]);

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("au-debug ready"));
    }

    [Test]
    public async Task Down_whenNotRunning_printsMessage()
    {
        using var output = new StringWriter();
        var exit = await Controller(output).RunAsync(["down"]);

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("not running"));
    }

    [Test]
    public async Task DesktopScreenshot_routesToDesktop()
    {
        using var output = new StringWriter();
        var exit = await Controller(output).RunAsync(["desktop", "screenshot"]);

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("screenshot:"));
    }

    [Test]
    public async Task Status_routesToHost()
    {
        using var output = new StringWriter();
        var exit = await Controller(output).RunAsync(["status"]);

        Assert.That(exit, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("not running"));
    }

    private static HostController Controller(StringWriter output)
    {
        var sessions = new FakeSessionStore();
        var supervisor = new FakeSupervisor();
        var probe = new FakeReadyProbe();
        var windows = new FakeDesktopWindowDriver();
        var debugOutput = new DebugOutputService(output);
        var host = new HostCommandService(sessions, supervisor, probe, windows, debugOutput);
        var desktop = new DesktopController(
            new DesktopCommandService(windows, new FakeWorkspaceClient(), sessions, new FakeEnvironment()));
        var mobile = new MobileController(
            new MobileCommandService(new FakeWebScreenshotDriver(), new FakeMobileSurfaceDriver(), sessions, new FakeEnvironment()));
        var docs = new DocsController(
            new DocsCommandService(new FakeWebScreenshotDriver(), sessions));
        return new HostController(host, desktop, mobile, docs, new DebugArgParser(), debugOutput);
    }
}
