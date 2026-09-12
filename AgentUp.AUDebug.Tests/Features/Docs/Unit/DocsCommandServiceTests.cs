using AgentUp.AUDebug.Features.Docs.Services;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Docs.Unit;

[TestFixture]
public sealed class DocsCommandServiceTests
{
    [Test]
    public async Task Screenshot_capturesDesignSystem()
    {
        var shots = new FakeWebScreenshotDriver();
        var result = await new DocsCommandService(shots, new FakeSessionStore())
            .ScreenshotAsync(
                new DebugCommandDto("docs", "docs", "screenshot", null, null, TimeSpan.FromSeconds(30), false),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(shots.Captures[0].Url, Is.EqualTo($"{DebugLayout.DocsUrl}{DebugLayout.DocsPath}#catalog"));
    }

    [Test]
    public async Task Screenshot_mapsDriverErrors()
    {
        var shots = new ThrowingShots();
        var result = await new DocsCommandService(shots, new FakeSessionStore())
            .ScreenshotAsync(
                new DebugCommandDto("docs", "docs", "screenshot", null, null, TimeSpan.FromSeconds(30), false),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("boom"));
    }

    private sealed class ThrowingShots : AgentUp.AUDebug.Shared.Interfaces.IWebScreenshotDriver
    {
        public Task CaptureAsync(string url, string outputPath, CancellationToken cancellationToken, string? userDataDirectory = null)
            => throw new InvalidOperationException("boom");
    }
}
