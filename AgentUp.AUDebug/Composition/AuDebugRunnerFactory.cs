using AgentUp.AUDebug.Features.Desktop.Controllers;
using AgentUp.AUDebug.Features.Desktop.Providers;
using AgentUp.AUDebug.Features.Desktop.Services;
using AgentUp.AUDebug.Features.Docs.Controllers;
using AgentUp.AUDebug.Features.Docs.Providers;
using AgentUp.AUDebug.Features.Docs.Services;
using AgentUp.AUDebug.Features.Host.Controllers;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Providers;
using AgentUp.AUDebug.Features.Host.Services;
using AgentUp.AUDebug.Features.Mobile.Controllers;
using AgentUp.AUDebug.Features.Mobile.Providers;
using AgentUp.AUDebug.Features.Mobile.Services;
using AgentUp.AUDebug.Features.Screens.Controllers;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Features.Screens.Services;
using AgentUp.AUDebug.Features.Screenshots.Controllers;
using AgentUp.AUDebug.Features.Screenshots.Providers;
using AgentUp.AUDebug.Features.Screenshots.Services;
using AgentUp.AUDebug.Features.Test.Controllers;
using AgentUp.AUDebug.Features.Test.Providers;
using AgentUp.AUDebug.Features.Test.Services;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Composition;

public static class AuDebugRunnerFactory
{
    public static HostController Create(string workingDirectory, TextWriter? output = null)
    {
        var writer = output ?? Console.Out;
        var repositoryRoot = RepositoryRootProvider.Find(workingDirectory)
            ?? throw new InvalidOperationException("Could not find agent-up.sln. Run au-debug from the Agent-Up repository.");
        var paths = new DebugPathValidator(repositoryRoot);
        var environment = new DebugEnvironment();
        var processes = new AllowlistedProcessRunner();
        var sessions = new HostSessionStore(paths);
        var outputService = new DebugOutputService(writer);
        var windows = new XdoToolDesktopDriver(processes, environment, paths);
        var probe = new HostReadyProbeProvider(new HttpClient { Timeout = TimeSpan.FromSeconds(2) });
        var supervisor = new HostProcessSupervisor(processes, paths, environment, probe, writer);
        var host = new HostCommandService(
            sessions,
            supervisor,
            probe,
            windows,
            outputService);
        var screenshots = new ChromiumScreenshotDriver(processes, environment, paths);
        var workspaces = new DesktopWorkspaceApiClient(new HttpClient
        {
            BaseAddress = new Uri(DebugLayout.ServerUrl),
            Timeout = TimeSpan.FromSeconds(DebugLayout.MaxTimeoutSeconds)
        });
        var desktop = new DesktopController(
            new DesktopCommandService(
                windows,
                workspaces,
                sessions,
                environment));
        var mobile = new MobileController(
            new MobileCommandService(
                screenshots,
                new ChromiumMobileDriver(processes, environment, paths),
                workspaces,
                sessions,
                environment));
        var docs = new DocsController(
            new DocsCommandService(
                new ChromiumDocsPageDriver(processes, environment, paths),
                sessions));
        var tests = new TestController(
            new TestCommandService(
                new DebugTestSuiteCatalog(),
                new DebugTestProcessRunner(processes, paths),
                writer));
        var screenshotMedia = new ScreenshotMediaStore(paths);
        var screenshotsController = new ScreenshotsController(
            new ScreenshotCommandService(
                new ScreenshotManifestStore(paths),
                screenshotMedia,
                screenshots,
                new ScreenshotAppContractProvider(paths, screenshotMedia),
                new ScreenshotPngComparer(),
                new ChromiumLiveAppProbe(processes, environment, paths),
                sessions,
                probe));
        var screens = new ScreensController(
            new ScreensCommandService(
                new ProductScreenCatalog(),
                new ScreenCaptureStore(paths),
                new ScreenSurfaceHostProvider(
                    processes,
                    environment,
                    paths,
                    new ScreenReadyProbeProvider(new HttpClient { Timeout = TimeSpan.FromSeconds(2) }, processes, environment, paths)),
                surface => surface == ProductSurface.Desktop
                    ? new DesktopScreenSurface(processes, environment, paths)
                    : new MobileScreenSurface(processes, environment, paths),
                new ScreenRunStore(paths),
                new DocumentedScreensProvider(paths),
                new ScreenComparisonProvider()));
        return new HostController(host, desktop, mobile, docs, tests, screenshotsController, screens, new DebugArgParser(), outputService);
    }
}
