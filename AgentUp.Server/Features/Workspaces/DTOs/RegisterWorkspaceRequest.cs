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

    /// <summary>
    /// The name is only the repository's, from <c>agent-up.json</c> <c>name</c>, so it does not
    /// replace the name a registered workspace already has. Several team members check out the
    /// same repository; starting one must not rename it after the repository.
    /// </summary>
    public bool NameIsRepositoryName { get; init; }
    public bool Derived { get; init; }
    public string? SourceRepository { get; init; }
    public string? SourceRevision { get; init; }
}
