using AgentUp.Server.Features.Ports.Models;

namespace AgentUp.Server.Features.Applications.DTOs;

public record DockerCapabilityDefinition(
    string Name,
    string Image,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? Volumes = null);
