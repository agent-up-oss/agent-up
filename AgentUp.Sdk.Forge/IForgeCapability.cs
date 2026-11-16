using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Forge;

/// <summary>
/// A repository-hosting module. Reads proposal state and performs the writes the
/// autonomous loop needs. It must not merge, and it must not write a protected ref.
/// </summary>
public interface IForgeCapability
{
    CapabilityIdentity Identity { get; }

    ForgeHostConfiguration Host { get; }

    Task<ForgeHostSnapshot> ReadAsync(ForgeRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForgeIssue>> ListAssignedAsync(
        ForgeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every open issue on the request's own repository, newest activity first. Assignment is not
    /// a filter here: a host that cannot assign to an agent still has issues worth picking, and
    /// the queue decides what is worked on rather than the host.
    /// </summary>
    Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
        ForgeRequest request,
        CancellationToken cancellationToken = default);

    Task<ForgeProposal> OpenProposalAsync(
        ForgeRequest request,
        ForgeOpenProposalCommand command,
        CancellationToken cancellationToken = default);

    Task UpdateRefAsync(
        ForgeRequest request,
        ForgeUpdateRefCommand command,
        CancellationToken cancellationToken = default);

    Task CommentAsync(
        ForgeRequest request,
        ForgeCommentCommand command,
        CancellationToken cancellationToken = default);

    Task SubmitReviewAsync(
        ForgeRequest request,
        ForgeSubmitReviewCommand command,
        CancellationToken cancellationToken = default);
}
