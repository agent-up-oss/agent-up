using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Applications.Unit;

[TestFixture]
public sealed class ApplicationLifecycleServiceTests
{
    [Test]
    public async Task Start_preparesADesktopApplicationBeforeLaunch()
    {
        var (service, registry, processes) = await CreateAsync();
        var workspace = registry.GetAll().Single();

        var result = await service.StartAsync(workspace.Id, "Editor");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(processes.Launched, Is.EqualTo(["Editor"]));
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Running));
            Assert.That(workspace.Applications.Single().RuntimeEnvironment["DISPLAY"], Is.EqualTo(":123"));
        });

        await service.StopAsync(workspace.Id, "Editor");
    }

    [Test]
    public async Task Restart_preparesADesktopApplicationAgain()
    {
        var (service, registry, processes) = await CreateAsync();
        var workspace = registry.GetAll().Single();
        await service.StartAsync(workspace.Id, "Editor");

        var result = await service.RestartAsync(workspace.Id, "Editor");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(processes.Killed, Is.EqualTo(["Editor"]));
            Assert.That(processes.Launched, Is.EqualTo(["Editor", "Editor"]));
            Assert.That(workspace.Applications.Single().RuntimeEnvironment["DISPLAY"], Is.EqualTo(":123"));
        });

        await service.StopAsync(workspace.Id, "Editor");
    }

    [Test]
    public async Task Stop_tearsDownTheDesktopSession()
    {
        var (service, registry, _) = await CreateAsync();
        var workspace = registry.GetAll().Single();
        await service.StartAsync(workspace.Id, "Editor");

        var result = await service.StopAsync(workspace.Id, "Editor");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Stopped));
        });
    }

    [Test]
    public async Task Start_returnsNotFoundForAnUnknownApplication()
    {
        var (service, registry, _) = await CreateAsync();

        var result = await service.StartAsync(registry.GetAll().Single().Id, "missing");

        Assert.That(result.Found, Is.False);
    }

    private static async Task<(ApplicationLifecycleService Service, AgentUp.Server.Features.Workspaces.Services.WorkspaceRegistry Registry, RecordingProcessManager Processes)> CreateAsync()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            DesktopApplications = [new DesktopApplicationDefinition("Editor", "dotnet run", ".")]
        });
        var processes = new RecordingProcessManager();
        var desktop = new DesktopApplicationsController(new DesktopSessionService(
            new FakeDesktopDisplayProvider(),
            new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new FakeHostedDesktopNativeLibraryProvider(),
            NullLogger<DesktopSessionService>.Instance));
        var service = new ApplicationLifecycleService(
            new AgentUp.Server.Features.Workspaces.Controllers.WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry),
            ServerTestComposition.CreateProcessesController(processes),
            desktop);
        return (service, registry, processes);
    }

    private sealed class RecordingProcessManager : IWorkspaceProcessManager
    {
        public List<string> Launched { get; } = [];
        public List<string> Killed { get; } = [];

        public Task LaunchAsync(Workspace workspace) => Task.CompletedTask;
        public Task LaunchApplicationAsync(Workspace workspace, string appName)
        {
            Launched.Add(appName);
            return Task.CompletedTask;
        }

        public Task KillAsync(string workspaceId) => Task.CompletedTask;
        public Task KillApplicationAsync(string workspaceId, string appName)
        {
            Killed.Add(appName);
            return Task.CompletedTask;
        }
    }
}
