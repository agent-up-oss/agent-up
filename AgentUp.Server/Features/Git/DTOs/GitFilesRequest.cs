namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitFilesRequest(IReadOnlyList<string>? Files);

public sealed record GitBranchRequest(string? Name, bool Create);

public sealed record GitCheckoutRequest(string? Name);

public sealed record GitFetchRequest(string? Remote);

public sealed record GitPullRequest(bool Rebase);

public sealed record GitPushRequest(bool ForceWithLease, bool SetUpstream);
