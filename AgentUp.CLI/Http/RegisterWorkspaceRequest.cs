using AgentUp.CLI.Models;

namespace AgentUp.CLI.Http;

public record RegisterWorkspaceRequest(
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit)
{
    public IReadOnlyList<ApplicationDefinition> Applications { get; init; } = [];
    public IReadOnlyList<DockerServiceDefinition> Services { get; init; } = [];
}
