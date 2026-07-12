namespace AgentUp.Server.Features.Applications.DTOs;

// Input DTO — definition only, no runtime state
public record ApplicationDefinition(
    string Name,
    string Command,
    string? Path,
    string? PortVariable);
