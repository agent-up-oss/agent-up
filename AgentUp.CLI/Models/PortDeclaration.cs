namespace AgentUp.CLI.Models;

public record PortDeclaration(string? Variable, int DefaultPort, string Protocol = "http");
