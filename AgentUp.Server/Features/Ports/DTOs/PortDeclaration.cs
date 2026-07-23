namespace AgentUp.Server.Features.Ports.DTOs;

public record PortDeclaration(
    string? Variable,
    int DefaultPort,
    string Protocol = "http");
