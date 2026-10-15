namespace AgentUp.Sdk.Forge;

public sealed record ForgeHostSnapshot(
    ForgeProposal? Proposal,
    IReadOnlyList<ForgeCheck> Checks,
    IReadOnlyList<ForgeReviewThread> Threads,
    ForgeMergeability? Mergeability);

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
