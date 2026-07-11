namespace AgentUp.Server.Features.Workspaces.DTOs;

public record ApplicationDefinition(
    string Name,
    string Command,
    string? Path,
    string? PortVariable);
