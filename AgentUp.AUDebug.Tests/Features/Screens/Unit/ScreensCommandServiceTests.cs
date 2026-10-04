using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Screens.Unit;

[TestFixture]
public sealed class ScreensCommandServiceTests
{
    [Test]
    public async Task Capture_runsEveryRouteAndWritesOneFilePerScreen()
    {
        var surface = new FakeProductScreenSurface(ProductSurface.Desktop);
        var store = new FakeScreenCaptureStore();
        var service = Service(Catalog(Screen("git"), Screen("history")), store, new FakeScreenSurfaceHost(), surface);

        var result = await service.CaptureAsync(DebugDomain.Screens(ProductSurface.Desktop).Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(surface.Opened, Is.True);
            Assert.That(surface.Steps, Has.Count.EqualTo(2));
            Assert.That(store.Manifest!.Screens.Select(screen => screen.View), Is.EqualTo(new[] { "git", "history" }));
        });
    }

    [Test]
    public async Task Capture_recordsAnUnavailableScreenWithItsReasonInsteadOfAFile()
    {
        var surface = new FakeProductScreenSurface(ProductSurface.Desktop);
        var store = new FakeScreenCaptureStore();
        var hidden = Screen("validation") with { Available = false, UnavailableReason = "Demo hides it" };
        var service = Service(Catalog(hidden), store, new FakeScreenSurfaceHost(), surface);

        await service.CaptureAsync(DebugDomain.Screens(ProductSurface.Desktop).Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(store.Manifest!.Screens.Single().File, Is.Null);
            Assert.That(store.Manifest.Screens.Single().SkippedReason, Is.EqualTo("Demo hides it"));
            Assert.That(surface.Captures, Is.Empty);
        });
    }

    [Test]
    public async Task Capture_selectsOneScreenByView()
    {
        var surface = new FakeProductScreenSurface(ProductSurface.Desktop);
        var store = new FakeScreenCaptureStore();
        var service = Service(Catalog(Screen("git"), Screen("history")), store, new FakeScreenSurfaceHost(), surface);

        await service.CaptureAsync(DebugDomain.Screens(ProductSurface.Desktop).ForView("history").Build(), CancellationToken.None);

        Assert.That(store.Manifest!.Screens.Select(screen => screen.View), Is.EqualTo(new[] { "history" }));
    }

    [Test]
    public async Task Capture_reportsAnUnknownView()
    {
        var service = Service(
            Catalog(Screen("git")),
            new FakeScreenCaptureStore(),
            new FakeScreenSurfaceHost(),
            new FakeProductScreenSurface(ProductSurface.Desktop));

        var result = await service.CaptureAsync(
            DebugDomain.Screens(ProductSurface.Desktop).ForView("nope").Build(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("unknown desktop screen 'nope'"));
        });
    }

    [Test]
    public async Task Capture_startsEverySurfaceWhenNoneIsNamed()
    {
        var host = new FakeScreenSurfaceHost();
        var catalog = new FakeProductScreenCatalog();
        catalog.Add(ProductSurface.Desktop, Screen("git"));
        catalog.Add(ProductSurface.Mobile, Screen("git") with { Surface = ProductSurface.Mobile });
        var service = Service(catalog, new FakeScreenCaptureStore(), host, new FakeProductScreenSurface(ProductSurface.Desktop));

        await service.CaptureAsync(DebugDomain.Screens().Build(), CancellationToken.None);

        Assert.That(host.Started, Is.EqualTo(new[] { ProductSurface.Desktop, ProductSurface.Mobile }));
    }

    [Test]
    public async Task Capture_reportsATimeoutRatherThanThrowing()
    {
        var surface = new FakeProductScreenSurface(ProductSurface.Desktop) { DelayUntilCanceled = true };
        var service = Service(Catalog(Screen("git")), new FakeScreenCaptureStore(), new FakeScreenSurfaceHost(), surface);

        var result = await service.CaptureAsync(
            DebugDomain.Screens(ProductSurface.Desktop).TimingOutAfter(TimeSpan.FromMilliseconds(80)).Build(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("Timed out"));
        });
    }


    [Test]
    public async Task CompareAsync_withoutARun_saysToCaptureFirst()
    {
        var service = Service(Catalog(), new FakeScreenCaptureStore(), new FakeScreenSurfaceHost(), new FakeProductScreenSurface(ProductSurface.Mobile));

        var result = await service.CompareAsync(DebugDomain.ScreensCompare().Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("Run au-debug screens first"));
        });
    }

    [Test]
    public async Task CompareAsync_realScreenMissingDocumentedCopy_failsAndNamesTheScreen()
    {
        var documented = new FakeDocumentedScreens();
        documented.Scenes.Add(new ScreenshotSceneCopyDto("mobile-git", "mobile", ["Commit message"]));
        var runs = Run(new ScreenCaptureDto("mobile-git", "mobile", "git", "Mobile Git", "git.png", null, "Changes"));
        var service = Service(Catalog(), new FakeScreenCaptureStore(), new FakeScreenSurfaceHost(), new FakeProductScreenSurface(ProductSurface.Mobile), runs, documented);

        var result = await service.CompareAsync(DebugDomain.ScreensCompare().Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("mobile-git"));
            Assert.That(result.Message, Does.Contain("'Commit message'"));
        });
    }

    [Test]
    public async Task CompareAsync_everyScreenMatching_succeeds()
    {
        var documented = new FakeDocumentedScreens();
        documented.Scenes.Add(new ScreenshotSceneCopyDto("mobile-git", "mobile", ["Commit message"]));
        var runs = Run(new ScreenCaptureDto("mobile-git", "mobile", "git", "Mobile Git", "git.png", null, "Changes\nCommit message"));
        var service = Service(Catalog(), new FakeScreenCaptureStore(), new FakeScreenSurfaceHost(), new FakeProductScreenSurface(ProductSurface.Mobile), runs, documented);

        var result = await service.CompareAsync(DebugDomain.ScreensCompare().Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Message, Does.Contain("1 matched"));
        });
    }

    [Test]
    public async Task CompareAsync_scopedToASurface_leavesTheOtherAlone()
    {
        var documented = new FakeDocumentedScreens();
        documented.Scenes.Add(new ScreenshotSceneCopyDto("mobile-git", "mobile", ["Commit message"]));
        documented.Scenes.Add(new ScreenshotSceneCopyDto("desktop-git", "desktop", ["Git changes"]));
        var runs = Run(new ScreenCaptureDto("desktop-git", "desktop", "git", "Desktop Git changes", "git.png", null, "Git changes"));
        var service = Service(Catalog(), new FakeScreenCaptureStore(), new FakeScreenSurfaceHost(), new FakeProductScreenSurface(ProductSurface.Desktop), runs, documented);

        var result = await service.CompareAsync(DebugDomain.ScreensCompare(ProductSurface.Desktop).Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Message, Does.Contain("Compared 1 documented screen"));
        });
    }

    private static FakeScreenRunStore Run(params ScreenCaptureDto[] captures)
        => new() { Manifest = new ScreenRunManifestDto("Demo", "harbor-shop", captures) };




    [Test]
    public async Task CaptureAsync_screenIdenticalToTheOneBefore_failsInsteadOfPhotographingIt()
    {
        var store = new FakeScreenCaptureStore { UnchangedFromPrevious = true };
        var catalog = Catalog(Screen("sign-in"), Screen("workspaces"));
        var service = Service(catalog, store, new FakeScreenSurfaceHost(), new FakeProductScreenSurface(ProductSurface.Desktop));

        var result = await service.CaptureAsync(DebugDomain.Screens().Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("identical to the screen before it"));
            Assert.That(result.Message, Does.Contain("DesktopScreenGeometry"));
        });
    }

    private static ScreensCommandService Service(
        IProductScreenCatalog catalog,
        IScreenCaptureStore store,
        IScreenSurfaceHost host,
        FakeProductScreenSurface surface,
        IScreenRunStore? runs = null,
        IDocumentedScreens? documented = null)
        => new(
            catalog,
            store,
            host,
            _ => surface,
            runs ?? new FakeScreenRunStore(),
            documented ?? new FakeDocumentedScreens(),
            new ScreenComparisonProvider());

    private static FakeProductScreenCatalog Catalog(params ProductScreenDto[] screens)
    {
        var catalog = new FakeProductScreenCatalog();
        catalog.Add(ProductSurface.Desktop, screens);
        return catalog;
    }

    private static ProductScreenDto Screen(string view)
        => new()
        {
            Id = $"{ProductSurface.Desktop}-{view}",
            Surface = ProductSurface.Desktop,
            View = view,
            Title = view,
            Steps = [ScreenStepDto.Settle(0)]
        };
}
