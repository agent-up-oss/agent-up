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
    public IReadOnlyList<DockerServiceDefinition> Services { get; init; } = [];
    public IReadOnlyList<DotnetApplicationDefinition> Dotnet { get; init; } = [];
    public IReadOnlyList<DockerCapabilityDefinition> Docker { get; init; } = [];
}
