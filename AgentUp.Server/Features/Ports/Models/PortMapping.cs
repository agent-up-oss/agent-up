namespace AgentUp.Server.Features.Ports.Models;

public record PortMapping(
    string? Variable,
    int DefaultPort,
    int AllocatedPort,
    string Protocol = "http");
