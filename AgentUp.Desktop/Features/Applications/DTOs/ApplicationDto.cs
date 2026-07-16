using AgentUp.Desktop.Features.Ports.DTOs;

namespace AgentUp.Desktop.Features.Applications.DTOs;

public sealed record ApplicationDto(
    string Name,
    string Command,
    string? Path,
    string State)
{
    public List<PortMappingDto> AllocatedPorts { get; init; } = [];
}
