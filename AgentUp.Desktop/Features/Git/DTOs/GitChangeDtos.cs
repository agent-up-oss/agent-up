namespace AgentUp.Desktop.Features.Git.DTOs;

public sealed record GitChangeTreeDto(
    string WorkspaceId,
    string Branch,
    int FileCount,
    GitChangeDirectoryDto Root,
    IReadOnlyList<string>? LocalBranches = null,
    IReadOnlyList<GitRemoteBranchDto>? RemoteBranches = null,
    string? Upstream = null,
    int Ahead = 0,
    int Behind = 0,
    string? Commit = null);

public sealed record GitChangeDirectoryDto(
    string Name,
    string Path,
    IReadOnlyList<GitChangeDirectoryDto> Directories,
    IReadOnlyList<GitChangeFileDto> Files);

public sealed record GitChangeFileDto(
    string Name,
    string Path,
    string Status);

public sealed record GitRemoteBranchDto(string Remote, string Name);

public sealed record GitHeadStateDto(
    string Branch,
    IReadOnlyList<string> LocalBranches,
    IReadOnlyList<GitRemoteBranchDto>? RemoteBranches = null,
    string? Upstream = null,
    int Ahead = 0,
    int Behind = 0,
    string? Commit = null);

public sealed record GitSyncResultDto(bool Found, bool Succeeded, string? Error, GitHeadStateDto? Head);

public sealed record GitLogDto(IReadOnlyList<GitLogCommitDto> Commits);

public sealed record GitLogCommitDto(
    string Id,
    string ShortId,
    IReadOnlyList<string> Parents,
    string Subject,
    string Author,
    string Timestamp,
    IReadOnlyList<string> Refs);

public sealed record GitLogRowDto(
    GitLogCommitDto Commit,
    int Lane,
    IReadOnlyList<int> ParentLanes,
    string Graph,
    string? CheckoutName);

public sealed record GitCheckoutRequestDto(string Name);

public sealed record GitFetchRequestDto(string? Remote);

public sealed record GitPullRequestDto(bool Rebase);

public sealed record GitPushRequestDto(bool ForceWithLease, bool SetUpstream);

public sealed record GitBranchChoiceDto(string Key, string Label, string Kind);
