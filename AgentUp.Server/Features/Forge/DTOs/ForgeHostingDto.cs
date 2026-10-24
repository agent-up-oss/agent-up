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
public sealed record ForgeReviewDto(string Login, string State);

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

public sealed record ForgeReviewThreadDto(string Id, string Url, string Summary, bool IsResolved);

public sealed record ForgeMergeabilityDto(bool Mergeable, string State);

public sealed record ForgeLoginStartDto(string AuthorizationUrl);

public sealed record ForgeHttpOutcome(int StatusCode, object? Body);

public sealed record ForgeOpenProposalRequest(string Title, string Body, string HeadRef, string BaseRef);

public sealed record ForgeUpdateRefRequest(string Ref, string Sha);

public sealed record ForgeCommentRequest(string ProposalNumber, string Body);

public sealed record ForgeReviewRequest(string ProposalNumber, string Body, string Event);
