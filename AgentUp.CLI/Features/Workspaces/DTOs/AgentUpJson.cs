namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record AgentUpJson(
    string Name,
    List<ApplicationDefinition>? Applications = null,
    List<DesktopApplicationDefinition>? DesktopApplications = null,
    List<DockerServiceDefinition>? Services = null,
    List<DotnetApplicationDefinition>? Dotnet = null,
    List<DockerCapabilityDefinition>? Docker = null,
    List<RuntimeSectionDefinition>? RuntimeSections = null,
    WorkspaceDisplayOptions? Display = null);

public sealed record WorkspaceDisplayOptions(
    string? Name = null,
    string? Branch = null);
