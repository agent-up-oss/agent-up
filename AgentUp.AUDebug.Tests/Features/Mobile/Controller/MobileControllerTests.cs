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
}
