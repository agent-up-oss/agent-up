namespace AgentUp.Server.Features.Training.Interfaces;

/// <summary>
/// Which repository a workspace is working on.
///
/// A decision belongs to a repository, and the agent recording it should not get to say which:
/// it is filed against whatever is actually checked out. This is the whole of what the backlog
/// needs to know about forges.
/// </summary>
public interface IDecisionRepositoryResolver
{
    (string Owner, string Repository)? Resolve(string workspaceId);
}
