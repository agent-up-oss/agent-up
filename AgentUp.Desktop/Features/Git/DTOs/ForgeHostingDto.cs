namespace AgentUp.Desktop.Features.Git.DTOs;

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
