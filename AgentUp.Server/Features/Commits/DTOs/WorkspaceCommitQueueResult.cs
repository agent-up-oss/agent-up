namespace AgentUp.Server.Features.Commits.DTOs;

public sealed record WorkspaceCommitQueueResult(bool Found, CommitsStatusResult? Queue)
{
    public static WorkspaceCommitQueueResult Missing() => new(false, null);
    public static WorkspaceCommitQueueResult Success(CommitsStatusResult queue) => new(true, queue);
}
