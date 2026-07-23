using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Features.Capabilities.Controllers;

public sealed class CapabilitiesController
{
    private readonly CapabilityReconciliationService _service;

    public CapabilitiesController(CapabilityReconciliationService service)
    {
        _service = service;
    }

    public async Task<ApplicationInstance> ReconcileDotnetAsync(
        DotnetApplicationDefinition definition,
        IReadOnlyList<PortDeclaration> ports,
        IReadOnlyList<PortMapping> allocatedPorts)
        => await _service.ReconcileDotnetAsync(definition, ports, allocatedPorts);

    public async Task<ApplicationInstance> ReconcileDockerAsync(
        DockerCapabilityDefinition definition,
        IReadOnlyList<PortDeclaration> ports,
        IReadOnlyList<PortMapping> allocatedPorts)
        => await _service.ReconcileDockerAsync(definition, ports, allocatedPorts);
}
