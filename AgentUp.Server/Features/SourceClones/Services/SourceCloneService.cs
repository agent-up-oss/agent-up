using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.SourceClones.Services;

public sealed class SourceCloneService : IHostedService
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
        _workspaces.WorkspaceDeleting += OnWorkspaceDeleting;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _workspaces.WorkspaceDeleting -= OnWorkspaceDeleting;
        return Task.CompletedTask;
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

    public async Task<SourceCloneResult> RefreshAsync(
        string workspaceId,
        string? requestedRevision = null,
        CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.GetById(workspaceId);
        if (workspace is null)
            return SourceCloneResult.Failed("Workspace was not found.");
        if (!workspace.Derived)
            return SourceCloneResult.Failed("Only derived review workspaces can be refreshed in place.");
        if (!_root.IsUnderRoot(workspace.WorktreePath))
            return SourceCloneResult.Failed("The workspace is not a managed source clone.");

        string revision;
        try
        {
            revision = string.IsNullOrWhiteSpace(requestedRevision)
                ? workspace.SourceRevision ?? workspace.Branch
                : _targets.RequireRevision(requestedRevision);
        }
        catch (InvalidOperationException ex)
        {
            return SourceCloneResult.Failed(ex.Message);
        }

        try
        {
            await _git.RefreshAsync(workspace.WorktreePath, revision, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return SourceCloneResult.Failed(ex.Message);
        }

        var identity = await _identity.ReadAsync(workspace.WorktreePath, cancellationToken);
        var branch = DisplayBranch(identity.Branch, revision);
        await _workspaces.UpdateDerivedRevisionAsync(
            workspace.Id,
            $"Review · {revision}",
            revision,
            branch,
            identity.Commit);
        return SourceCloneResult.Success(_workspaces.GetById(workspace.Id) ?? workspace);
    }

    private async Task<RegisterWorkspaceRequest> BuildRegistrationAsync(
        SourceCloneTarget target,
        CancellationToken cancellationToken)
    {
        var declared = await _registration.BuildAsync(target.DestinationPath, cancellationToken);
        var identity = await _identity.ReadAsync(target.DestinationPath, cancellationToken);
        var branch = DisplayBranch(identity.Branch, target.Branch);
        var displayName = target.Derived
            ? $"Review · {target.Branch}"
            : declared?.DisplayName ?? target.DirectoryName;

        if (declared is not null && !target.Derived)
            return declared with { Branch = branch, Commit = identity.Commit };

        return new RegisterWorkspaceRequest(
            DisplayName: displayName,
            RepositoryPath: identity.RepositoryPath,
            WorktreePath: target.DestinationPath,
            Branch: branch,
            Commit: identity.Commit)
        {
            Applications = declared?.Applications ?? [],
            DesktopApplications = declared?.DesktopApplications ?? [],
            Services = declared?.Services ?? [],
            Dotnet = declared?.Dotnet ?? [],
            Docker = declared?.Docker ?? [],
            RuntimeSections = declared?.RuntimeSections ?? [],
            Derived = target.Derived,
            SourceRepository = target.Derived ? target.Repository : null,
            SourceRevision = target.Derived ? target.Branch : null
        };
    }

    private void OnWorkspaceDeleting(Workspace workspace)
    {
        if (!workspace.Derived)
            return;

        _root.DeleteIfUnderRoot(workspace.WorktreePath);
    }

    private static string DisplayBranch(string identityBranch, string requestedRevision)
        => string.Equals(identityBranch, "HEAD", StringComparison.Ordinal)
            ? requestedRevision
            : identityBranch;
}
