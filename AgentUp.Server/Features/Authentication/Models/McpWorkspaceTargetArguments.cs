namespace AgentUp.Server.Features.Authentication.Models;

public static class McpWorkspaceTargetArguments
{
    public const string WorkspaceId = "workspaceId";
    public const string WorktreePath = "worktreePath";
    public const string RepositoryPath = "repositoryPath";
    public const string Id = "id";

    public static readonly IReadOnlyList<string> Names =
    [
        WorkspaceId,
        WorktreePath,
        RepositoryPath,
        Id
    ];

    public static bool IsPathName(string name)
        => NamesEqual(name, WorktreePath) || NamesEqual(name, RepositoryPath);

    public static bool IsAlwaysBoundName(string name)
        => NamesEqual(name, WorkspaceId) || IsPathName(name);

    public static bool IsWorkspaceIdName(string name, string? description)
        => NamesEqual(name, WorkspaceId)
           || (NamesEqual(name, Id)
               && description is not null
               && description.Contains("workspace", StringComparison.OrdinalIgnoreCase));

    private static bool NamesEqual(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
