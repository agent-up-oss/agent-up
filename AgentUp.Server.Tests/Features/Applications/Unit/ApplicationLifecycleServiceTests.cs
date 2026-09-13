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
            Assert.That(((RecordingProcessManager)processes).Launched, Is.EqualTo(["Editor"]));
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
            Assert.That(((RecordingProcessManager)processes).Killed, Is.EqualTo(["Editor"]));
            Assert.That(((RecordingProcessManager)processes).Launched, Is.EqualTo(["Editor", "Editor"]));
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

    [Test]
    public async Task Start_marksTheDesktopApplicationFailedWhenLaunchThrows()
    {
        var (service, registry, _) = await CreateAsync(new ThrowingProcessManager());
        var workspace = registry.GetAll().Single();

        var result = await service.StartAsync(workspace.Id, "Editor");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo("Application could not be started."));
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
        });
    }

    [Test]
    public async Task Stop_marksTheDesktopApplicationFailedWhenKillThrows()
    {
        var (service, registry, _) = await CreateAsync();
        var workspace = registry.GetAll().Single();
        await service.StartAsync(workspace.Id, "Editor");
        var (failing, _, _) = await CreateAsync(new ThrowingProcessManager(), registry);

        var result = await failing.StopAsync(workspace.Id, "Editor");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo("Application could not be stopped."));
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
        });
    }

    [Test]
    public async Task Restart_marksTheDesktopApplicationFailedWhenLaunchThrows()
    {
        var (service, registry, _) = await CreateAsync();
        var workspace = registry.GetAll().Single();
        await service.StartAsync(workspace.Id, "Editor");
        var (failing, _, _) = await CreateAsync(new ThrowingProcessManager(), registry);

        var result = await failing.RestartAsync(workspace.Id, "Editor");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo("Application could not be restarted."));
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
        });
    }

    private static async Task<(ApplicationLifecycleService Service, AgentUp.Server.Features.Workspaces.Services.WorkspaceRegistry Registry, IWorkspaceProcessManager Processes)> CreateAsync(
        IWorkspaceProcessManager? processes = null,
        AgentUp.Server.Features.Workspaces.Services.WorkspaceRegistry? registry = null)
    {
        registry ??= ServerTestComposition.CreateRegistry();
        if (registry.GetAll().Count == 0)
        {
            await registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
            {
                DesktopApplications = [new DesktopApplicationDefinition("Editor", "dotnet run", ".")]
            });
        }
        processes ??= new RecordingProcessManager();
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

    private sealed class ThrowingProcessManager : IWorkspaceProcessManager
    {
        public Task LaunchAsync(Workspace workspace) => Task.CompletedTask;
        public Task LaunchApplicationAsync(Workspace workspace, string appName) =>
            throw new InvalidOperationException("launch exploded");
        public Task KillAsync(string workspaceId) => Task.CompletedTask;
        public Task KillApplicationAsync(string workspaceId, string appName) =>
            throw new InvalidOperationException("kill exploded");
    }
}
