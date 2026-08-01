using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Tests.Fake;

internal static class ServerTestComposition
{
    public static WorkspaceRegistry CreateRegistry(IReadOnlyList<ICapabilityAdapter>? adapters = null)
        => new(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService(adapters ?? [])),
            new WorkspaceEventBus());

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
            new WorkspaceStateController(registry),
            CreateProcessesController(processes),
            configuration,
            identity));

    public static WorkspaceStateController CreateWorkspaceStateController(WorkspaceRegistry registry)
        => new(registry);

    public static AuditController CreateAuditController(
        WorkspaceRegistry? registry = null,
        InMemoryAuditEventRepository? events = null,
        InMemoryAuditArtifactRepository? artifacts = null,
        FakeAuditIdentityProvider? identity = null)
        => new(new AuditService(
            events ?? new InMemoryAuditEventRepository(),
            artifacts ?? new InMemoryAuditArtifactRepository(),
            identity ?? new FakeAuditIdentityProvider(),
            new WorkspaceQueryController(registry ?? CreateRegistry())));
}
