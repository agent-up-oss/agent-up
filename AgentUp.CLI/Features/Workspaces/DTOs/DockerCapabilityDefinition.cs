namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record DockerCapabilityDefinition(
    string Name,
    string Image,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? Volumes = null,
    IReadOnlyList<string>? EnvironmentFiles = null);
