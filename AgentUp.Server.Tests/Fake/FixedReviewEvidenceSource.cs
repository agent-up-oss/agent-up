using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Tests.Fake;

/// <summary>Captures by artifact id, as the audit store would hold them after browser_screenshot.</summary>
internal sealed class FixedReviewEvidenceSource : IReviewEvidenceSource
{
    public Dictionary<string, (string MimeType, byte[] Bytes)> Captures { get; } = new(StringComparer.Ordinal);

    public Task<(string MimeType, byte[] Bytes)?> ReadAsync(string artifactId, CancellationToken cancellationToken) =>
        Task.FromResult<(string MimeType, byte[] Bytes)?>(Captures.TryGetValue(artifactId, out var capture) ? capture : null);
}
