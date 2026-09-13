namespace AgentUp.Server.Features.Commits.Interfaces;

public interface ICommitQueueConfigurationProvider
{
    bool IsGitQueueEnabled(string worktreePath);
}
