using AgentUp.AUDebug.Features.Docs.Services;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Docs.Unit;

[TestFixture]
public sealed class DocsCommandServiceTests
{
    [Test]
    public async Task Screenshot_capturesDocsHome()
    {
        var pages = new FakeDocsPageCapture();
        var result = await new DocsCommandService(pages, new FakeSessionStore())
            .ScreenshotAsync(
                DebugDomain.Command(DebugDomain.DocsSurface)
                    .Doing(DebugDomain.ScreenshotAction)
                    .Build(),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(pages.Captures[0].Url, Is.EqualTo($"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}"));
        Assert.That(pages.Captures[0].Heading, Is.Null);
        Assert.That(pages.Captures[0].FullPage, Is.False);
    }

    [Test]
    public async Task Screenshot_forwardsPathHeadingAndFullPage()
    {
        var pages = new FakeDocsPageCapture();
        var result = await new DocsCommandService(pages, new FakeSessionStore())
            .ScreenshotAsync(
                DebugDomain.Command(DebugDomain.DocsSurface)
                    .Doing(DebugDomain.ScreenshotAction)
                    .AtPage("/developer-guide/git")
                    .ScrollingTo("What it is")
                    .CapturingFullPage()
                    .Build(),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(pages.Captures[0].Url, Is.EqualTo($"{DebugLayout.DocsUrl}/developer-guide/git"));
        Assert.That(pages.Captures[0].Heading, Is.EqualTo("What it is"));
        Assert.That(pages.Captures[0].FullPage, Is.True);
    }

    [Test]
    public async Task Screenshot_mapsInvalidPaths()
    {
        var pages = new FakeDocsPageCapture();
        var result = await new DocsCommandService(pages, new FakeSessionStore())
            .ScreenshotAsync(
                DebugDomain.Command(DebugDomain.DocsSurface)
                    .Doing(DebugDomain.ScreenshotAction)
                    .AtPage("../secret")
                    .Build(),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("not a hosted"));
        Assert.That(pages.Captures, Is.Empty);
    }

    [Test]
    public async Task Screenshot_mapsDriverErrors()
    {
        var pages = new FakeDocsPageCapture { CaptureException = new InvalidOperationException("boom") };
        var result = await new DocsCommandService(pages, new FakeSessionStore())
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
        var pages = new FakeDocsPageCapture { DelayUntilCanceled = true };
        var result = await new DocsCommandService(pages, new FakeSessionStore())
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
