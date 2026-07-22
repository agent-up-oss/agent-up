namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record DotnetApplicationDefinition(
    string Name,
    string? Sdk,
    DotnetRunDefinition Run,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? EnvironmentFiles = null);
