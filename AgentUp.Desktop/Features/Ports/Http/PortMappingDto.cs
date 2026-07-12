namespace AgentUp.Desktop.Features.Ports.Http;

public sealed record PortMappingDto(
    string? Variable,
    int DefaultPort,
    int AllocatedPort);
