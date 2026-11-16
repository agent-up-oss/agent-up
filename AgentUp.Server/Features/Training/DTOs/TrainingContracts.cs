using AgentUp.Server.Features.Training.Models;

namespace AgentUp.Server.Features.Training.DTOs;

/// <summary>One decision, as a review card shows it.</summary>
public sealed record ProductDecisionDto(
    string Id,
    string Owner,
    string Repository,
    string WorkspaceId,
    string Agent,
    string Category,
    string Headline,
    string Detail,
    string Evidence,
    IReadOnlyList<DecisionEvidenceDto> EvidenceImages,
    DateTimeOffset RecordedAt,
    string Verdict,
    string? Note,
    DateTimeOffset? ReviewedAt);

/// <summary>A picture behind a decision, as a surface asks for it.</summary>
public sealed record DecisionEvidenceDto(string EvidenceId, string SourceArtifactId, string MimeType);

/// <summary>
/// The picture itself, carried as text.
///
/// A surface cannot put an authenticated request behind an image tag, so the bytes come back
/// through the same authenticated read as everything else and are rendered from there.
/// </summary>
public sealed record DecisionEvidenceImageDto(string EvidenceId, string MimeType, string Base64);

/// <summary>
/// One repository's tile on the Train tab: who it is and how much is waiting, counted per
/// category so a reader can see where the backlog actually is before opening anything.
/// </summary>
public sealed record DecisionRepositoryDto(
    string Owner,
    string Repository,
    int Pending,
    int Reviewed,
    IReadOnlyList<DecisionCategoryCountDto> Categories);

/// <summary>How many decisions sit in one category of one repository.</summary>
public sealed record DecisionCategoryCountDto(string Category, string Label, int Pending, int Reviewed);

/// <summary>What an agent recorded, before the Server gave it an id and a time.</summary>
public sealed record RecordDecisionRequest(
    string WorkspaceId,
    string Category,
    string Headline,
    string Detail,
    string Evidence,
    IReadOnlyList<string>? EvidenceArtifactIds = null,
    string? Owner = null,
    string? Repository = null,
    string? Agent = null);

/// <summary>The product owner's verdict on one decision, with whatever they wanted to add.</summary>
public sealed record ReviewDecisionRequest(string Verdict, string? Note = null);

/// <summary>
/// What came of a recording. A refused draft says why per field, and a repeat says which
/// decision it already matches, because both are things the agent can act on.
/// </summary>
public sealed record RecordDecisionResult(
    bool Recorded,
    string Message,
    ProductDecisionDto? Decision = null,
    IReadOnlyList<DecisionFaultDto>? Faults = null,
    string? DuplicateOfId = null);

/// <summary>One thing wrong with a draft decision.</summary>
public sealed record DecisionFaultDto(string Field, string Problem);
