namespace AgentUp.Server.Features.Forge.DTOs;

public sealed record ForgeHostingDto(
    string Availability,
    string? ModuleId,
    ForgeProposalDto? Proposal,
    IReadOnlyList<ForgeCheckDto> Checks,
    IReadOnlyList<ForgeReviewThreadDto> Threads,
    ForgeMergeabilityDto? Mergeability,
    string? Detail,
    string? Remedy);

public sealed record ForgeProposalDto(
    string Id,
    string Number,
    string Title,
    string Url,
    string HeadRef,
    string BaseRef,
    string State);

public sealed record ForgeCheckDto(string Name, string Status, string Conclusion, string Url);

public sealed record ForgeReviewThreadDto(string Id, string Url, string Summary, bool IsResolved);

public sealed record ForgeMergeabilityDto(bool Mergeable, string State);

public sealed record ForgeLoginStartDto(string AuthorizationUrl);

public sealed record ForgeHttpOutcome(int StatusCode, object? Body);

public sealed record ForgeOpenProposalRequest(string Title, string Body, string HeadRef, string BaseRef);

public sealed record ForgeUpdateRefRequest(string Ref, string Sha);

public sealed record ForgeCommentRequest(string ProposalNumber, string Body);

public sealed record ForgeReviewRequest(string ProposalNumber, string Body, string Event);

/// <summary>Submitted is false when the workspace has no usable forge; Refusal is set when its team member may not review.</summary>
public sealed record ForgeReviewOutcome(bool Submitted, string? Refusal);
