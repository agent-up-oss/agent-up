using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Forge;

/// <summary>
/// A repository-hosting module. Reads proposal state and performs the writes the
/// autonomous loop needs. It must not merge, and it must not write a protected ref.
/// </summary>
public interface IForgeCapability : ICapability
{
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

    /// <summary>
    /// Assigns an issue to the account <paramref name="login"/>: the team member that took the task.
    /// A host that cannot assign leaves the issue as it is.
    /// </summary>
    Task AssignIssueAsync(
        ForgeRequest request,
        int issueNumber,
        string login,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Names <paramref name="login"/> as a reviewer the proposal is waiting on. A host that cannot
    /// request reviewers leaves the proposal as it is.
    /// </summary>
    Task RequestReviewAsync(
        ForgeRequest request,
        string proposalNumber,
        string login,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Stores a file against an issue or proposal, such as a screenshot a review points at, and
    /// returns where a reader opens it. Null where the host offers no upload API.
    /// </summary>
    Task<ForgeAttachment?> AttachAsync(
        ForgeRequest request,
        string issueNumber,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default) => Task.FromResult<ForgeAttachment?>(null);
}
