using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Features.Applications.DTOs;

public record DockerServiceDefinition(
    string Name,
    string Image,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? Volumes = null,
    IReadOnlyList<string>? EnvironmentFiles = null);
