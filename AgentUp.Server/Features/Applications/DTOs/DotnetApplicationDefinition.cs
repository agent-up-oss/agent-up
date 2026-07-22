using AgentUp.Server.Features.Ports.Models;

namespace AgentUp.Server.Features.Applications.DTOs;

public record DotnetApplicationDefinition(
    string Name,
    string? Sdk,
    DotnetRunDefinition Run,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? EnvironmentFiles = null);
