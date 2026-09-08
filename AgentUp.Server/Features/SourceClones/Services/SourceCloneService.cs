using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.SourceClones.Services;

public sealed class SourceCloneService
{
    private readonly ISourceCloneTargetProvider _targets;
    private readonly ISourceCloneGitProvider _git;
    private readonly ISourceCloneRootProvider _root;
    private readonly IWorkspaceIdentityProvider _identity;
    private readonly OrchestrationRegistrationController _registration;
    private readonly WorkspaceQueryController _workspaces;

    public SourceCloneService(
        ISourceCloneTargetProvider targets,
        ISourceCloneGitProvider git,
        ISourceCloneRootProvider root,
        IWorkspaceIdentityProvider identity,
        OrchestrationRegistrationController registration,
        WorkspaceQueryController workspaces)
    {
        _targets = targets;
        _git = git;
        _root = root;
        _identity = identity;
        _registration = registration;
        _workspaces = workspaces;
    }

    public SourceCloneRoot GetRoot() => new(_root.GetRoot());

    public async Task<SourceCloneResult> CloneAsync(
        CloneSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        SourceCloneTarget target;
        try
        {
            target = _targets.Resolve(request);
        }
        catch (InvalidOperationException ex)
        {
            return SourceCloneResult.Failed(ex.Message);
        }

        if (_targets.DestinationExists(target))
            return SourceCloneResult.Failed($"A source clone directory named '{target.DirectoryName}' already exists.");

        try
        {
            await _git.CloneAsync(target, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return SourceCloneResult.Failed(ex.Message);
        }

        var registration = await BuildRegistrationAsync(target, cancellationToken);
        var workspace = await _workspaces.RegisterAsync(registration);
        return SourceCloneResult.Success(workspace);
    }

    private async Task<RegisterWorkspaceRequest> BuildRegistrationAsync(
        SourceCloneTarget target,
        CancellationToken cancellationToken)
    {
        var declared = await _registration.BuildAsync(target.DestinationPath, cancellationToken);
        if (declared is not null)
            return declared;

        var identity = await _identity.ReadAsync(target.DestinationPath, cancellationToken);
        return new RegisterWorkspaceRequest(
            DisplayName: target.DirectoryName,
            RepositoryPath: identity.RepositoryPath,
            WorktreePath: target.DestinationPath,
            Branch: identity.Branch,
            Commit: identity.Commit);
    }
}
