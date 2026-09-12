using AgentUp.AUDebug.Features.Docs.Controllers;
using AgentUp.AUDebug.Features.Docs.Services;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Docs.Controller;

[TestFixture]
public sealed class DocsControllerTests
{
    [Test]
    public async Task Screenshot_returnsPath()
    {
        var shots = new FakeWebScreenshotDriver();
        var result = await new DocsController(
            new DocsCommandService(shots, new FakeSessionStore())).ScreenshotAsync(
            new DebugCommandDto("docs", "docs", "screenshot", null, null, TimeSpan.FromSeconds(30), false),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(shots.Captures[0].Url, Does.Contain("/design-system"));
        Assert.That(result.ArtifactPath, Is.Not.Null);
    }

    [Test]
    public async Task Screenshot_mapsDriverErrors()
    {
        var shots = new FakeWebScreenshotDriver { CaptureException = new InvalidOperationException("boom") };
        var result = await new DocsController(
            new DocsCommandService(shots, new FakeSessionStore())).ScreenshotAsync(
            new DebugCommandDto("docs", "docs", "screenshot", null, null, TimeSpan.FromSeconds(30), false),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("boom"));
    }
}
