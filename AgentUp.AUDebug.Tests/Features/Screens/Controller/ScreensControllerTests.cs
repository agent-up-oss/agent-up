using AgentUp.AUDebug.Features.Screens.Controllers;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Features.Screens.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Screens.Controller;

[TestFixture]
public sealed class ScreensControllerTests
{
    [Test]
    public async Task Run_capturesTheSurfaceTheCommandNames()
    {
        var host = new FakeScreenSurfaceHost();
        var controller = Controller(host, out _);

        var result = await controller.RunAsync(DebugDomain.Screens(ProductSurface.Mobile).Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(host.Started, Is.EqualTo(new[] { ProductSurface.Mobile }));
        });
    }

    [Test]
    public async Task Run_reportsWhereTheManifestLandedSoTheArtifactCanBeFound()
    {
        var controller = Controller(new FakeScreenSurfaceHost(), out var store);

        var result = await controller.RunAsync(DebugDomain.Screens(ProductSurface.Mobile).Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ArtifactPath, Is.EqualTo("artifacts/product-screens/screens.json"));
            Assert.That(store.Manifest!.Server, Is.EqualTo("Demo"));
        });
    }

    [Test]
    public async Task Run_surfacesACaptureFailureAsANonZeroExit()
    {
        var surface = new FakeProductScreenSurface(ProductSurface.Mobile)
        {
            CaptureException = new InvalidOperationException("no window")
        };
        var catalog = new FakeProductScreenCatalog();
        catalog.Add(ProductSurface.Mobile, Screen());
        var controller = new ScreensController(
            new ScreensCommandService(
                catalog, new FakeScreenCaptureStore(), new FakeScreenSurfaceHost(), _ => surface,
                new FakeScreenRunStore(),
                new FakeDocumentedScreens(),
                new ScreenComparisonProvider()));

        var result = await controller.RunAsync(DebugDomain.Screens(ProductSurface.Mobile).Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("no window"));
        });
    }

    private static ScreensController Controller(FakeScreenSurfaceHost host, out FakeScreenCaptureStore store)
    {
        store = new FakeScreenCaptureStore();
        var catalog = new FakeProductScreenCatalog();
        catalog.Add(ProductSurface.Mobile, Screen());
        return new ScreensController(
            new ScreensCommandService(
                catalog,
                store,
                host,
                surface => new FakeProductScreenSurface(surface),
                new FakeScreenRunStore(),
                new FakeDocumentedScreens(),
                new ScreenComparisonProvider()));
    }

    private static ProductScreenDto Screen()
        => new()
        {
            Id = "mobile-git",
            Surface = ProductSurface.Mobile,
            View = "git",
            Title = "Mobile Git",
            Steps = [ScreenStepDto.Settle(0)]
        };
}
