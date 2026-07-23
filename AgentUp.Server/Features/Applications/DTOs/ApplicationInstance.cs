using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Capabilities.DTOs;

namespace AgentUp.Server.Features.Applications.DTOs;

public class ApplicationInstance
{
    public required string Name { get; init; }
    public ServiceType ServiceType { get; init; } = ServiceType.Process;

    // Process fields
    public string? Command { get; init; }
    public string? Path { get; init; }
    public IReadOnlyList<string>? EnvironmentFiles { get; init; }

    // Process and Docker fields
    public string? Image { get; init; }
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
    public IReadOnlyList<string>? Volumes { get; init; }

    // Capability reconciliation fields
    public string? CapabilityId { get; init; }
    public string? CapabilityVersionRequirement { get; init; }
    public CapabilityStatusDto? CapabilityStatus { get; init; }

    // Port declarations (shared by process and docker apps)
    public IReadOnlyList<PortDeclaration> Ports { get; init; } = [];
    public IReadOnlyList<PortMapping> AllocatedPorts { get; set; } = [];

    public ApplicationState State { get; set; } = ApplicationState.Stopped;
}
