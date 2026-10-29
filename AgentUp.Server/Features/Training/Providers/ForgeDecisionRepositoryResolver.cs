using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Training.Interfaces;

namespace AgentUp.Server.Features.Training.Providers;

/// <summary>Answers from the forge the workspace is actually checked out on.</summary>
public sealed class ForgeDecisionRepositoryResolver(ForgeController forge) : IDecisionRepositoryResolver
{
    public (string Owner, string Repository)? Resolve(string workspaceId)
    {
        var repository = forge.ResolveRepository(workspaceId);
        return repository is null ? null : (repository.Owner, repository.Repository);
    }
}
