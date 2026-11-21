using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Tests.Fake;

/// <summary>Records the pages a reviewer's browser was asked to open, refusing them when told to.</summary>
internal sealed class RecordingReviewBrowser(string? refusal = null) : IReviewBrowser
{
    public List<(string WorkspaceId, string Url)> Opened { get; } = [];

    public Task<string?> OpenAsync(string workspaceId, string url, CancellationToken cancellationToken)
    {
        Opened.Add((workspaceId, url));
        return Task.FromResult(refusal);
    }
}
