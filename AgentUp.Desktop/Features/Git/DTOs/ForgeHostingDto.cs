namespace AgentUp.Desktop.Features.Git.DTOs;

public sealed record ForgeHostingDto(
    string Availability,
    string? ModuleId,
    ForgeProposalDto? Proposal,
    IReadOnlyList<ForgeCheckDto> Checks,
    IReadOnlyList<ForgeReviewThreadDto> Threads,
    ForgeMergeabilityDto? Mergeability,
    string? Detail,
    ForgeAccountDto? Account = null);

/// <summary>
/// The account this workspace acts as on its host. It belongs to the workspace rather than to the
/// person operating it, so everything the agent pushes, comments, or reviews is attributable to it.
/// </summary>
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
