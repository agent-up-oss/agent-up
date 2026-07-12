using AgentUp.Desktop.Features.Ports.Http;

namespace AgentUp.Desktop.Features.Applications.Http;

public sealed record ApplicationDto(
    string Name,
    string Command,
    string? Path,
    string State)
{
    public List<PortMappingDto> AllocatedPorts { get; init; } = [];
}
