using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Controllers;
using AgentUp.AUDebug.Features.Mobile.Services;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Mobile.Controller;

[TestFixture]
public sealed class MobileControllerTests
{
    [Test]
    public async Task Screenshot_returnsPath()
    {
        var screenshots = new FakeWebScreenshotDriver();
        var result = await new MobileController(
            new MobileCommandService(screenshots, new FakeMobileSurfaceDriver(), new FakeSessionStore(), new FakeEnvironment())).RunAsync(
            new DebugCommandDto("mobile", "mobile", "screenshot", null, null, TimeSpan.FromSeconds(30), false),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(screenshots.Captures, Has.Count.EqualTo(1));
        Assert.That(result.ArtifactPath, Is.Not.Null);
    }

    [Test]
    public async Task Login_routesToSurface()
    {
        var surface = new FakeMobileSurfaceDriver();
        var result = await new MobileController(
            new MobileCommandService(new FakeWebScreenshotDriver(), surface, new FakeSessionStore(), new FakeEnvironment())).RunAsync(
            new DebugCommandDto("mobile", "mobile", "login", null, "test", TimeSpan.FromSeconds(30), false),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(surface.Password, Is.EqualTo("test"));
    }

    [Test]
    public async Task UnknownAction_fails()
    {
        var result = await new MobileController(
            new MobileCommandService(new FakeWebScreenshotDriver(), new FakeMobileSurfaceDriver(), new FakeSessionStore(), new FakeEnvironment())).RunAsync(
            new DebugCommandDto("mobile", "mobile", "nope", null, null, TimeSpan.FromSeconds(30), false),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("unknown mobile action"));
    }
}
