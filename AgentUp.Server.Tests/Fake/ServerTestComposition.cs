using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Mcp.Controllers;
using AgentUp.Server.Features.Mcp.Interfaces;
using AgentUp.Server.Features.Mcp.Services;
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
            new CapabilitiesController(new CapabilityReconciliationService(adapters ?? [])));

    public static ProcessesController CreateProcessesController(
        IWorkspaceProcessManager processes,
        IOutputRepository? output = null)
        => new(processes, new ProcessOutputService(output ?? new InMemoryOutputRepository()));

    public static McpWorkspaceController CreateMcpWorkspaceController(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processes,
        IAgentUpConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity)
        => new(new McpWorkspaceService(
            new WorkspaceQueryController(registry),
            new WorkspaceStateController(registry),
            CreateProcessesController(processes),
            configuration,
            identity));

    public static WorkspaceStateController CreateWorkspaceStateController(WorkspaceRegistry registry)
        => new(registry);
}
