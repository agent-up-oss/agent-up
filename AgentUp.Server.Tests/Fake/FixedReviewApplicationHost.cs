using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Tests.Fake;

/// <summary>Answers a product reviewer's start with fixed addresses, or with a refusal.</summary>
internal sealed class FixedReviewApplicationHost(IReadOnlyList<string> addresses, string? error = null) : IReviewApplicationHost
{
    public List<string> Started { get; } = [];

    public Task<(IReadOnlyList<string> Addresses, string? Error)> RunAsync(string workspaceId, CancellationToken cancellationToken)
    {
        Started.Add(workspaceId);
        return Task.FromResult<(IReadOnlyList<string>, string?)>(error is null ? (addresses, null) : ([], error));
    }
}
