namespace AgentUp.Server.Features.Ports.DTOs;

public record PortMapping(
    string? Variable,
    int DefaultPort,
    int AllocatedPort,
    string Protocol = "http");
