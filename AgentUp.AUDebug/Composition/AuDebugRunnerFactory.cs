using AgentUp.AUDebug.Features.Desktop.Controllers;
using AgentUp.AUDebug.Features.Desktop.Providers;
using AgentUp.AUDebug.Features.Desktop.Services;
using AgentUp.AUDebug.Features.Docs.Controllers;
using AgentUp.AUDebug.Features.Docs.Services;
using AgentUp.AUDebug.Features.Host.Controllers;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Providers;
using AgentUp.AUDebug.Features.Host.Services;
using AgentUp.AUDebug.Features.Mobile.Controllers;
using AgentUp.AUDebug.Features.Mobile.Providers;
using AgentUp.AUDebug.Features.Mobile.Services;
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
        var supervisor = new HostProcessSupervisor(processes, paths, environment, writer);
        var host = new HostCommandService(
            sessions,
            supervisor,
            new HostReadyProbeProvider(new HttpClient { Timeout = TimeSpan.FromSeconds(2) }),
            windows,
            outputService);
        var screenshots = new ChromiumScreenshotDriver(processes, environment, paths);
        var desktop = new DesktopController(
            new DesktopCommandService(
                windows,
                new DesktopWorkspaceApiClient(new HttpClient
                {
                    BaseAddress = new Uri(DebugLayout.ServerUrl),
                    Timeout = TimeSpan.FromSeconds(DebugLayout.DefaultTimeoutSeconds)
                }),
                sessions,
                environment));
        var mobile = new MobileController(
            new MobileCommandService(
                screenshots,
                new ChromiumMobileDriver(processes, environment, paths),
                sessions,
                environment));
        var docs = new DocsController(new DocsCommandService(screenshots, sessions));
        return new HostController(host, desktop, mobile, docs, new DebugArgParser(), outputService);
    }
}
