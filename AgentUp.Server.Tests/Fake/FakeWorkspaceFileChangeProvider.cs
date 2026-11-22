using AgentUp.Server.Features.Files.Interfaces;
using AgentUp.Server.Features.Git.DTOs;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeWorkspaceFileChangeProvider : IWorkspaceFileChangeProvider
{
    public List<GitChangeEntry> Changes { get; } = [];

    public Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(string workspaceId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<GitChangeEntry>>(Changes);
}
