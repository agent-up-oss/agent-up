using AgentUp.Server.Features.Applications.DTOs;

namespace AgentUp.Server.Features.Orchestration.DTOs;

public sealed record AgentUpConfiguration(
    string Name,
    IReadOnlyList<ApplicationDefinition>? Applications = null,
    IReadOnlyList<DockerServiceDefinition>? Services = null);
