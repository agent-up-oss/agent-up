using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Features.Commits.Interfaces;

public interface IProposalStackGitProvider
{
    Task<ProposalCommitResult> EnqueueAsync(
        string worktreePath,
        CommitsQueue current,
        string queueId,
        string message,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken = default);
}
