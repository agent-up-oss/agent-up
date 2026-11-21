namespace AgentUp.Server.Features.Forge.DTOs;

public sealed record ForgeHostingDto(
    string Availability,
    string? ModuleId,
    ForgeProposalDto? Proposal,
    IReadOnlyList<ForgeCheckDto> Checks,
    IReadOnlyList<ForgeReviewThreadDto> Threads,
    ForgeMergeabilityDto? Mergeability,
    string? Detail,
    string? Remedy,
    ForgeAccountDto? Account = null,
    IReadOnlyList<ForgeReviewDto>? Reviews = null);

/// <summary>A verdict left on the proposal; the state is the host's own word for it.</summary>
public sealed record ForgeReviewDto(string Login, string State, string? CommitId = null);

/// <summary>The account this workspace acts as on its host; null until the host names one.</summary>
public sealed record ForgeAccountDto(string Login, string DisplayName, bool IsBot, string Url);

public sealed record ForgeProposalDto(
    string Id,
    string Number,
    string Title,
    string Url,
    string HeadRef,
    string BaseRef,
    string State);

public sealed record ForgeCheckDto(string Name, string Status, string Conclusion, string Url);

/// <summary>A comment on the proposal; a finding also names the file, the line and who raised it.</summary>
public sealed record ForgeReviewThreadDto(string Id, string Url, string Summary, bool IsResolved, string? Path = null, int? Line = null, string? Author = null);

public sealed record ForgeMergeabilityDto(bool Mergeable, string State);

/// <summary>An open issue on the workspace's repository, offered for picking into the queue.</summary>
public sealed record ForgeIssueDto(
    string Id,
    int Number,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Url);

/// <summary>The owner and repository a workspace's checkout points at.</summary>
public sealed record ForgeRepositoryDto(string Owner, string Repository);

public sealed record ForgeLoginStartDto(string AuthorizationUrl);

public sealed record ForgeHttpOutcome(int StatusCode, object? Body);

public sealed record ForgeOpenProposalRequest(string Title, string Body, string HeadRef, string BaseRef);

public sealed record ForgeUpdateRefRequest(string Ref, string Sha);

public sealed record ForgeCommentRequest(string ProposalNumber, string Body);

public sealed record ForgeReviewRequest(string ProposalNumber, string Body, string Event, IReadOnlyList<ForgeReviewFindingDto>? Findings = null);

/// <summary>One review finding: a comment on one line of one changed file.</summary>
public sealed record ForgeReviewFindingDto(string Path, int Line, string Body);

/// <summary>
/// What the host says about one task branch's proposal: the commit it is at, its checks, every
/// comment and finding, and each reviewer's standing verdict. Null proposal means none is open.
/// </summary>
public sealed record ForgeTaskFeedbackDto(
    ForgeProposalDto? Proposal,
    string? HeadSha,
    IReadOnlyList<ForgeCheckDto> Checks,
    IReadOnlyList<ForgeReviewThreadDto> Threads,
    IReadOnlyList<ForgeReviewDto> Reviews);

/// <summary>Submitted is false when the workspace has no usable forge; Refusal is set when its team member may not review.</summary>
public sealed record ForgeReviewOutcome(bool Submitted, string? Refusal);
