using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Interfaces;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeSourceCloneRootProvider(string root) : ISourceCloneRootProvider
{
    public string GetRoot() => root;

    public bool IsUnderRoot(string path)
        => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
           || string.Equals(Path.GetFullPath(path), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);

    public List<string> DeletedPaths { get; } = [];

    public void DeleteIfUnderRoot(string path)
    {
        if (IsUnderRoot(path))
            DeletedPaths.Add(path);
    }
}

internal sealed class FakeSourceCloneTargetProvider : ISourceCloneTargetProvider
{
    public string? ResolveError { get; set; }

    public bool Exists { get; set; }

    public SourceCloneTarget Target { get; set; } =
        new("https://example.test/acme/widgets.git", "main", "widgets", "/clones/widgets");

    public CloneSourceRequest? LastRequest { get; private set; }

    public SourceCloneTarget Resolve(CloneSourceRequest request)
    {
        LastRequest = request;
        if (ResolveError is not null)
            throw new InvalidOperationException(ResolveError);

        var revision = FirstNonEmpty(request.Revision, request.Branch);
        return Target with
        {
            Derived = request.Derived,
            Branch = revision.Length > 0 ? revision : Target.Branch
        };
    }

    private static string FirstNonEmpty(string? first, string? second)
    {
        var left = (first ?? string.Empty).Trim();
        return left.Length > 0 ? left : (second ?? string.Empty).Trim();
    }

    public bool DestinationExists(SourceCloneTarget target) => Exists;
}

internal sealed class FakeSourceCloneGitProvider : ISourceCloneGitProvider
{
    public string? CloneError { get; set; }

    public string? RefreshError { get; set; }

    public SourceCloneTarget? Cloned { get; private set; }

    public (string DestinationPath, string Revision)? Refreshed { get; private set; }

    public Task CloneAsync(SourceCloneTarget target, CancellationToken cancellationToken = default)
    {
        Cloned = target;
        return CloneError is null
            ? Task.CompletedTask
            : Task.FromException(new InvalidOperationException(CloneError));
    }

    public Task RefreshAsync(string destinationPath, string revision, CancellationToken cancellationToken = default)
    {
        Refreshed = (destinationPath, revision);
        return RefreshError is null
            ? Task.CompletedTask
            : Task.FromException(new InvalidOperationException(RefreshError));
    }
}

internal sealed class FakeWorkspaceIdentityProvider(WorkspaceIdentity identity) : IWorkspaceIdentityProvider
{
    public WorkspaceIdentity Identity { get; set; } = identity;

    public Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken)
        => Task.FromResult(Identity);
}

internal sealed class FakeAgentUpConfigurationProvider(AgentUpConfiguration? configuration = null)
    : IAgentUpConfigurationProvider
{
    public Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken)
        => Task.FromResult(configuration);
}
