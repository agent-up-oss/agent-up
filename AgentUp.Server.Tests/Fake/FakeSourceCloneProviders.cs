using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Interfaces;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeSourceCloneRootProvider(string root) : ISourceCloneRootProvider
{
    public string GetRoot() => root;
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
        return ResolveError is null
            ? Target
            : throw new InvalidOperationException(ResolveError);
    }

    public bool DestinationExists(SourceCloneTarget target) => Exists;
}

internal sealed class FakeSourceCloneGitProvider : ISourceCloneGitProvider
{
    public string? CloneError { get; set; }

    public SourceCloneTarget? Cloned { get; private set; }

    public Task CloneAsync(SourceCloneTarget target, CancellationToken cancellationToken = default)
    {
        Cloned = target;
        return CloneError is null
            ? Task.CompletedTask
            : Task.FromException(new InvalidOperationException(CloneError));
    }
}

internal sealed class FakeWorkspaceIdentityProvider(WorkspaceIdentity identity) : IWorkspaceIdentityProvider
{
    public Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken)
        => Task.FromResult(identity);
}

internal sealed class FakeAgentUpConfigurationProvider(AgentUpConfiguration? configuration = null)
    : IAgentUpConfigurationProvider
{
    public Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken)
        => Task.FromResult(configuration);
}
