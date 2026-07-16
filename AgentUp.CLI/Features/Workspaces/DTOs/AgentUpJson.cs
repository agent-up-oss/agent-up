namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record AgentUpJson(
    string Name,
    List<ApplicationDefinition>? Applications = null,
    List<DockerServiceDefinition>? Services = null);
