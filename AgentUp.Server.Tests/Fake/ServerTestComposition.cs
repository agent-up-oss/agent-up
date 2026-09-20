using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.Providers;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.Interfaces;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Providers;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Fake;

/// <summary>
/// Composes the Server object graphs that are too large to assemble at every test: the
/// workspace registry, the lifecycle service and their collaborators.
/// </summary>
/// <remarks>
/// It composes services only. Test data is built through the <c>Support/</c> builders, so
/// nothing here decides what a subject is asked about - only what it is wired to. Every
/// parameter that changes behaviour under test is required rather than defaulted here, so
/// reading a call still tells you what the subject was given; the parameterless overloads
/// name the plain composition they stand for instead of hiding a choice inside a
/// null-coalescing default.
/// </remarks>
internal static class ServerTestComposition
{
    public static IServiceCollection AddWorkspaceLifecycleSupport(this IServiceCollection services)
    {
        services.AddSingleton<IAgentUpConfigurationProvider, AgentUpConfigurationProvider>();
        services.AddSingleton<IWorkspaceIdentityProvider, GitWorkspaceIdentityProvider>();
        services.AddSingleton<OrchestrationRegistrationService>();
        services.AddSingleton<OrchestrationRegistrationController>();
        services.AddSingleton<AppMetricsHttpClient>();
        services.AddSingleton<AppMetricsPullService>();
        services.AddSingleton<AppMetricsController>();
        services.AddSingleton<BrowserLifecycleController>();
        services.AddSingleton<PngFrameProvider>();
        services.AddSingleton<IDesktopDisplayProvider, FakeDesktopDisplayProvider>();
        services.AddSingleton<IHostedDesktopNativeLibraryProvider, FakeHostedDesktopNativeLibraryProvider>();
        services.AddSingleton<DesktopInputMessageProvider>();
        services.AddSingleton<DesktopViewerTicketProvider>();
        services.AddSingleton<DesktopSessionService>();
        services.AddHostedService(sp => sp.GetRequiredService<DesktopSessionService>());
        services.AddSingleton<DesktopApplicationsController>();
        services.AddSingleton<IWorkspaceDiskUsageProvider, WorkspaceDiskUsageProvider>();
        services.AddSingleton<WorkspaceOverviewService>();
        services.AddSingleton<WorkspaceLifecycleService>();
        services.AddSingleton<WorkspaceLifecycleController>();
        return services;
    }
    /// <summary>A registry with no capability adapters and an event bus of its own.</summary>
    public static WorkspaceRegistry CreateRegistry() => CreateRegistry([], new WorkspaceEventBus());

    public static WorkspaceRegistry CreateRegistry(
        IReadOnlyList<ICapabilityAdapter> adapters,
        WorkspaceEventBus bus)
        => new(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService(adapters)),
            bus);

    public static ProcessesController CreateProcessesController(IWorkspaceProcessManager processes)
        => CreateProcessesController(processes, new InMemoryOutputRepository());

    public static ProcessesController CreateProcessesController(
        IWorkspaceProcessManager processes,
        IOutputRepository output)
        => new(processes, new ProcessOutputService(output));

    public static OrchestrationWorkspaceController CreateOrchestrationWorkspaceController(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processes,
        IAgentUpConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity)
        => new(new OrchestrationWorkspaceService(
            new WorkspaceQueryController(registry),
            new WorkspaceStateController(registry, new WorkspaceEventBus()),
            new WorkspaceLifecycleController(CreateWorkspaceLifecycleService(
                registry, processes, configuration, identity, OperatingSystem.IsLinux)),
            new OrchestrationRegistrationService(
                configuration,
                identity)));

    /// <summary>
    /// The lifecycle service on the real configuration and identity providers, running as
    /// the host platform does.
    /// </summary>
    public static WorkspaceLifecycleService CreateWorkspaceLifecycleService(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processes)
        => CreateWorkspaceLifecycleService(
            registry,
            processes,
            new AgentUpConfigurationProvider(),
            new GitWorkspaceIdentityProvider(),
            OperatingSystem.IsLinux);

    /// <summary>
    /// The lifecycle service with the platform branch stated rather than read from the
    /// machine the suite happens to run on.
    /// </summary>
    public static WorkspaceLifecycleService CreateWorkspaceLifecycleService(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processes,
        Func<bool> isLinux)
        => CreateWorkspaceLifecycleService(
            registry,
            processes,
            new AgentUpConfigurationProvider(),
            new GitWorkspaceIdentityProvider(),
            isLinux);

    public static WorkspaceLifecycleService CreateWorkspaceLifecycleService(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processes,
        IAgentUpConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity,
        Func<bool> isLinux)
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var eventBus = new BrowserEventBus();
        var bus = new WorkspaceEventBus();
        var stateController = new WorkspaceStateController(registry, bus);
        var queryController = new WorkspaceQueryController(registry);
        var healthCheckService = new AppHealthCheckService(
            queryController, stateController, CreateAuditController(), NullLogger<AppHealthCheckService>.Instance);
        var healthChecks = new AppHealthController(healthCheckService);
        var metricsPulls = new AppMetricsController(new AppMetricsPullService(
            new AppMetricsHttpClient(), CreateAuditController(), NullLogger<AppMetricsPullService>.Instance));
        var streamState = new WorkspaceStreamStateService(
            eventBus, healthChecks, queryController, CreateAuditController(),
            NullLogger<WorkspaceStreamStateService>.Instance);
        var sessions = new HeadlessBrowserSessionManager(
            Path.GetTempPath(), Path.GetTempPath(), display,
            streamState,
            NullLogger<HeadlessBrowserSessionManager>.Instance);
        var browser = new BrowserLifecycleController(sessions, display);
        var desktop = new DesktopApplicationsController(new DesktopSessionService(
            new FakeDesktopDisplayProvider(),
            display,
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new FakeHostedDesktopNativeLibraryProvider(),
            NullLogger<DesktopSessionService>.Instance));
        var registration = new OrchestrationRegistrationService(configuration, identity);
        return new WorkspaceLifecycleService(
            registry,
            CreateProcessesController(processes),
            browser,
            desktop,
            healthChecks,
            metricsPulls,
            new WorkspaceStreamStateController(streamState),
            new OrchestrationRegistrationController(registration),
            NullLogger<WorkspaceLifecycleService>.Instance,
            isLinux);
    }

    public static WorkspaceStateController CreateWorkspaceStateController(WorkspaceRegistry registry)
        => new(registry, new WorkspaceEventBus());

    /// <summary>Stream state over a fresh registry and browser event bus.</summary>
    public static WorkspaceStreamStateService CreateStreamState()
        => CreateStreamState(new BrowserEventBus(), CreateRegistry());

    public static WorkspaceStreamStateService CreateStreamState(
        BrowserEventBus eventBus,
        WorkspaceRegistry registry)
    {
        var queryController = new WorkspaceQueryController(registry);
        var healthCheckService = new AppHealthCheckService(
            queryController,
            new WorkspaceStateController(registry, new WorkspaceEventBus()),
            CreateAuditController(),
            NullLogger<AppHealthCheckService>.Instance);
        var healthChecks = new AppHealthController(healthCheckService);
        return new WorkspaceStreamStateService(
            eventBus,
            healthChecks,
            queryController,
            CreateAuditController(),
            NullLogger<WorkspaceStreamStateService>.Instance);
    }

    /// <summary>The audit controller over in-memory storage and a fresh registry.</summary>
    public static AuditController CreateAuditController()
        => CreateAuditController(CreateRegistry());

    /// <summary>The audit controller over in-memory storage, reading the given registry.</summary>
    public static AuditController CreateAuditController(WorkspaceRegistry registry)
        => CreateAuditController(registry, new InMemoryAuditEventRepository());

    public static AuditController CreateAuditController(
        WorkspaceRegistry registry,
        IAuditEventRepository events)
        => CreateAuditController(registry, events, new InMemoryAuditArtifactRepository());

    public static AuditController CreateAuditController(
        WorkspaceRegistry registry,
        IAuditEventRepository events,
        IAuditArtifactRepository artifacts)
        => CreateAuditController(registry, events, artifacts, new FakeAuditIdentityProvider());

    public static AuditController CreateAuditController(
        WorkspaceRegistry registry,
        IAuditEventRepository events,
        IAuditArtifactRepository artifacts,
        FakeAuditIdentityProvider identity)
        => new(new AuditService(
            events,
            artifacts,
            identity,
            new WorkspaceQueryController(registry),
            new AuditEventBus()));
}
