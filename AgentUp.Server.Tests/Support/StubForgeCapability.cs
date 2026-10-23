using AgentUp.Sdk.Common;
using AgentUp.Sdk.Forge;

namespace AgentUp.Server.Tests.Support;

internal sealed class StubForgeCapability : IForgeCapability
{
    public CapabilityIdentity Identity { get; init; } = new("github", "1.0.0", "GitHub", "agent-up");

    public ForgeHostConfiguration Host { get; init; } = new(
        "https://example.test/api/",
        "https://example.test/login/oauth/authorize",
        "https://example.test/login/oauth/access_token");

    public ForgeHostSnapshot Snapshot { get; init; } = new(null, [], [], null);

    public IReadOnlyList<ForgeAssignedIssue> Assigned { get; init; } = [];

    public Exception? ReadFailure { get; init; }

    public Exception? ListAssignedFailure { get; init; }

    public Exception? UpdateRefFailure { get; init; }

    public ForgeProposal? Opened { get; init; }

    public int UpdateRefCalls { get; private set; }

    public int CommentCalls { get; private set; }

    public int ReviewCalls { get; private set; }

    public Task<ForgeHostSnapshot> ReadAsync(ForgeRequest request, CancellationToken cancellationToken = default)
        => ReadFailure is not null
            ? Task.FromException<ForgeHostSnapshot>(ReadFailure)
            : Task.FromResult(Snapshot);

    public Task<IReadOnlyList<ForgeAssignedIssue>> ListAssignedAsync(
        ForgeRequest request,
        CancellationToken cancellationToken = default)
        => ListAssignedFailure is not null
            ? Task.FromException<IReadOnlyList<ForgeAssignedIssue>>(ListAssignedFailure)
            : Task.FromResult(Assigned);

    public Task<ForgeProposal> OpenProposalAsync(
        ForgeRequest request,
        ForgeOpenProposalCommand command,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Opened ?? new ForgeProposal("1", "42", command.Title, "https://example.test/42", command.HeadRef, command.BaseRef, "open"));

    public Task UpdateRefAsync(ForgeRequest request, ForgeUpdateRefCommand command, CancellationToken cancellationToken = default)
    {
        if (UpdateRefFailure is not null)
            return Task.FromException(UpdateRefFailure);
        UpdateRefCalls++;
        return Task.CompletedTask;
    }

    public Task CommentAsync(ForgeRequest request, ForgeCommentCommand command, CancellationToken cancellationToken = default)
    {
        CommentCalls++;
        return Task.CompletedTask;
    }

    public Task SubmitReviewAsync(ForgeRequest request, ForgeSubmitReviewCommand command, CancellationToken cancellationToken = default)
    {
        ReviewCalls++;
        return Task.CompletedTask;
    }
}
