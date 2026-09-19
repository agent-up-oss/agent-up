using AgentUp.AUDebug.Features.Docs.Services;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

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
                DebugDomain.Command(DebugDomain.DocsSurface)
                    .Doing(DebugDomain.ScreenshotAction)
                    .Build(),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(shots.Captures[0].Url, Is.EqualTo($"{DebugLayout.DocsUrl}{DebugLayout.DocsPath}#catalog"));
    }

    [Test]
    public async Task Screenshot_mapsDriverErrors()
    {
        var shots = new FakeWebScreenshotDriver { CaptureException = new InvalidOperationException("boom") };
        var result = await new DocsCommandService(shots, new FakeSessionStore())
            .ScreenshotAsync(
                DebugDomain.Command(DebugDomain.DocsSurface)
                    .Doing(DebugDomain.ScreenshotAction)
                    .Build(),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("boom"));
    }

    [Test]
    public async Task Screenshot_timeout_returnsFailure()
    {
        var shots = new FakeWebScreenshotDriver { DelayUntilCanceled = true };
        var result = await new DocsCommandService(shots, new FakeSessionStore())
            .ScreenshotAsync(
                DebugDomain.Command(DebugDomain.DocsSurface)
                    .Doing(DebugDomain.ScreenshotAction)
                    .TimingOutAfter(TimeSpan.FromMilliseconds(30))
                    .Build(),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Timed out"));
    }
}
