using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Fake;

internal static class ServerTestComposition
{
    public static WorkspaceRegistry CreateRegistry(
        IReadOnlyList<ICapabilityAdapter>? adapters = null,
        WorkspaceEventBus? bus = null)
        => new(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService(adapters ?? [])),
            bus ?? new WorkspaceEventBus());

    public static ProcessesController CreateProcessesController(
        IWorkspaceProcessManager processes,
        IOutputRepository? output = null)
        => new(processes, new ProcessOutputService(output ?? new InMemoryOutputRepository()));

    public static OrchestrationWorkspaceController CreateOrchestrationWorkspaceController(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processes,
        IAgentUpConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity)
        => new(new OrchestrationWorkspaceService(
            new WorkspaceQueryController(registry),
            new WorkspaceStateController(registry, new WorkspaceEventBus()),
            CreateProcessesController(processes),
            CreateStreamStateController(registry: registry),
            configuration,
            identity));

    public static WorkspaceLifecycleService CreateWorkspaceLifecycleService(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processes)
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var eventBus = new BrowserEventBus();
        var bus = new WorkspaceEventBus();
        var stateController = new WorkspaceStateController(registry, bus);
        var queryController = new WorkspaceQueryController(registry);
        var healthCheckService = new AppHealthCheckService(
            queryController, stateController, CreateAuditController(), NullLogger<AppHealthCheckService>.Instance);
        var healthChecks = new AppHealthController(healthCheckService);
        var streamState = new WorkspaceStreamStateService(
            eventBus, healthChecks, queryController, CreateAuditController(),
            NullLogger<WorkspaceStreamStateService>.Instance);
        var sessions = new HeadlessBrowserSessionManager(
            Path.GetTempPath(), Path.GetTempPath(), display,
            streamState,
            NullLogger<HeadlessBrowserSessionManager>.Instance);
        var browser = new BrowserLifecycleController(sessions, display);
        return new WorkspaceLifecycleService(
            registry,
            CreateProcessesController(processes),
            browser,
            healthChecks,
            new WorkspaceStreamStateController(streamState),
            NullLogger<WorkspaceLifecycleService>.Instance);
    }

    public static WorkspaceStateController CreateWorkspaceStateController(WorkspaceRegistry registry)
        => new(registry, new WorkspaceEventBus());

    public static WorkspaceStreamStateController CreateStreamStateController(
        BrowserEventBus? eventBus = null,
        WorkspaceRegistry? registry = null)
        => new(CreateStreamState(eventBus, registry));

    public static WorkspaceStreamStateService CreateStreamState(
        BrowserEventBus? eventBus = null,
        WorkspaceRegistry? registry = null)
    {
        registry ??= CreateRegistry();
        var queryController = new WorkspaceQueryController(registry);
        var healthCheckService = new AppHealthCheckService(
            queryController,
            new WorkspaceStateController(registry, new WorkspaceEventBus()),
            CreateAuditController(),
            NullLogger<AppHealthCheckService>.Instance);
        var healthChecks = new AppHealthController(healthCheckService);
        return new WorkspaceStreamStateService(
            eventBus ?? new BrowserEventBus(),
            healthChecks,
            queryController,
            CreateAuditController(),
            NullLogger<WorkspaceStreamStateService>.Instance);
    }

    public static AuditController CreateAuditController(
        WorkspaceRegistry? registry = null,
        IAuditEventRepository? events = null,
        IAuditArtifactRepository? artifacts = null,
        FakeAuditIdentityProvider? identity = null)
        => new(new AuditService(
            events ?? new InMemoryAuditEventRepository(),
            artifacts ?? new InMemoryAuditArtifactRepository(),
            identity ?? new FakeAuditIdentityProvider(),
            new WorkspaceQueryController(registry ?? CreateRegistry())));
}
