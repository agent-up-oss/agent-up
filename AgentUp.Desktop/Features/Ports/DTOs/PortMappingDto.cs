namespace AgentUp.Desktop.Features.Ports.DTOs;

public sealed record PortMappingDto(
    string? Variable,
    int DefaultPort,
    int AllocatedPort,
    string Protocol = "http");
