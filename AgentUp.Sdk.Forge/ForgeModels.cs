namespace AgentUp.Sdk.Forge;

public sealed record ForgeHostSnapshot(
    ForgeProposal? Proposal,
    IReadOnlyList<ForgeCheck> Checks,
    IReadOnlyList<ForgeReviewThread> Threads,
    ForgeMergeability? Mergeability,
    ForgeAccount? Account = null,
    IReadOnlyList<ForgeReview>? Reviews = null);

/// <summary>
/// A verdict somebody left on the proposal. The state is the host's own word for it, because the
/// three a host distinguishes — approved, changes requested, commented — are exactly the three a
/// reader needs: settled, blocked, and something worth reading but not blocking.
/// </summary>
public sealed record ForgeReview(string Login, string State, DateTimeOffset SubmittedAt);

/// <summary>
/// The account a workspace acts as on its repository host. It belongs to the workspace, not to the
/// person operating it: the credential is per workspace, so everything an agent pushes, comments,
/// or reviews is attributable to this account and to no one else. A module that cannot name the
/// account leaves it null rather than guessing one.
/// </summary>
public sealed record ForgeAccount(string Login, string DisplayName, bool IsBot, string Url);

public sealed record ForgeProposal(
    string Id,
    string Number,
    string Title,
    string Url,
    string HeadRef,
    string BaseRef,
    string State);

public sealed record ForgeCheck(string Name, string Status, string Conclusion, string Url);

public sealed record ForgeReviewThread(string Id, string Url, string Summary, bool IsResolved);

public sealed record ForgeMergeability(bool Mergeable, string State);

public sealed record ForgeAssignedIssue(
    string Id,
    int Number,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Url);

public sealed record ForgeOpenProposalCommand(string Title, string Body, string HeadRef, string BaseRef);

public sealed record ForgeUpdateRefCommand(string Ref, string Sha);

public sealed record ForgeCommentCommand(string ProposalNumber, string Body);

public sealed record ForgeSubmitReviewCommand(string ProposalNumber, string Body, string Event);
