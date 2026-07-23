using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Features.Applications.DTOs;

// Input DTO — definition only, no runtime state
public record ApplicationDefinition(
    string Name,
    string Command,
    string? Path,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? EnvironmentFiles = null);
