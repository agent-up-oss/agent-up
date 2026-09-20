namespace AgentUp.Server.Features.Commits.Models;

public sealed record ProposalCommitResult(
    string BaseCommit,
    string ParentCommit,
    string Commit,
    string QueueWorktreePath,
    string QueueRef,
    string Patch);
