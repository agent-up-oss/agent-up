using AgentUp.Server.Features.Applications.DTOs;

namespace AgentUp.Server.Features.Workspaces.DTOs;

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
    public IReadOnlyList<DotnetApplicationDefinition> Dotnet { get; init; } = [];
    public IReadOnlyList<DockerCapabilityDefinition> Docker { get; init; } = [];
    public IReadOnlyList<RuntimeSectionDefinition> RuntimeSections { get; init; } = [];
    public bool Derived { get; init; }
    public string? SourceRepository { get; init; }
    public string? SourceRevision { get; init; }
}
