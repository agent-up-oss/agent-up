namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record PortDeclaration(string? Variable, int DefaultPort, string Protocol = "http");
