using AgentUp.Server.Features.Applications.DTOs;

namespace AgentUp.Server.Features.Mcp.DTOs;

public sealed record AgentUpConfiguration(
    string Name,
    IReadOnlyList<ApplicationDefinition>? Applications = null,
    IReadOnlyList<DockerServiceDefinition>? Services = null);
