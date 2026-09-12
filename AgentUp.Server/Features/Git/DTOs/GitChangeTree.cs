namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitChangeTree(
    string WorkspaceId,
    string Branch,
    int FileCount,
    GitChangeDirectory Root,
    IReadOnlyList<string> LocalBranches);

public sealed record GitChangeDirectory(
    string Name,
    string Path,
    IReadOnlyList<GitChangeDirectory> Directories,
    IReadOnlyList<GitChangeFile> Files);

public sealed record GitChangeFile(
    string Name,
    string Path,
    GitChangeStatus Status);
