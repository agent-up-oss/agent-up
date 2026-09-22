namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record RegisterWorkspaceRequest(
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit)
{
    public IReadOnlyList<ApplicationDefinition> Applications { get; init; } = [];
    public IReadOnlyList<DesktopApplicationDefinition> DesktopApplications { get; init; } = [];
    public IReadOnlyList<DockerServiceDefinition> Services { get; init; } = [];
    public IReadOnlyList<RuntimeSectionDefinition> RuntimeSections { get; init; } = [];
}
