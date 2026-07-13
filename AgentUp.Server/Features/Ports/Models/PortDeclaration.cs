namespace AgentUp.Server.Features.Ports.Models;

public record PortDeclaration(
    string? Variable,
    int DefaultPort,
    string Protocol = "http");
