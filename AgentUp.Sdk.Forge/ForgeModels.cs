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
public sealed record ForgeReview(string Login, string State, DateTimeOffset SubmittedAt, string? CommitId = null);

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
    string State,
    string? HeadSha = null);

public sealed record ForgeCheck(string Name, string Status, string Conclusion, string Url);

/// <summary>
/// A comment on the proposal. One left on a line of the change carries its <paramref name="Path"/>
/// and <paramref name="Line"/>; a conversation comment carries neither. <paramref name="Author"/> is
/// the account that wrote it, so a reader can tell a reviewer's finding from the author's reply.
/// </summary>
public sealed record ForgeReviewThread(
    string Id,
    string Url,
    string Summary,
    bool IsResolved,
    string? Path = null,
    int? Line = null,
    string? Author = null);

public sealed record ForgeMergeability(bool Mergeable, string State);

public sealed record ForgeIssue(
    string Id,
    int Number,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Url,
    string? AssigneeLogin = null);

public sealed record ForgeOpenProposalCommand(string Title, string Body, string HeadRef, string BaseRef);

public sealed record ForgeUpdateRefCommand(string Ref, string Sha);

public sealed record ForgeCommentCommand(string ProposalNumber, string Body);

/// <summary>One finding at a code location: a line of a file in the change, and what to do about it.</summary>
public sealed record ForgeReviewComment(string Path, int Line, string Body);

/// <summary>
/// A review of one proposal. <paramref name="CommitId"/> names the commit that was reviewed, so a
/// host that has not caught up with the latest push still files the verdict against the right
/// revision; without it the host uses whatever head it holds.
/// </summary>
public sealed record ForgeSubmitReviewCommand(
    string ProposalNumber,
    string Body,
    string Event,
    IReadOnlyList<ForgeReviewComment>? Comments = null,
    string? CommitId = null);

/// <summary>A file the host stores against an issue or proposal, and where a reader opens it.</summary>
public sealed record ForgeAttachment(string Name, string Url);

/// <summary>A repository the token can see. The owner and name are what later reads are addressed with.</summary>
public sealed record ForgeRepository(string Owner, string Name);

/// <summary>
/// An open pull request, the people asked to review it, and the reviews already left. The review
/// state is the host's own word for it.
/// </summary>
public sealed record ForgePullRequest(
    string Id,
    int Number,
    string Title,
    string Url,
    IReadOnlyList<string> RequestedReviewers,
    IReadOnlyList<ForgeReview> Reviews);

/// <summary>
/// What a personal access token can do for the task list. Both have to be true before the token
/// is kept: the repository list and the review tabs are read from it, and a token that cannot
/// see pull requests cannot say which reviews are still owed.
/// </summary>
public sealed record ForgeTokenAccess(bool CanListRepositories, bool CanReadPullRequests, string? Detail);
