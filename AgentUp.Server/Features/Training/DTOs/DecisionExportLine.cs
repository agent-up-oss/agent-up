namespace AgentUp.Server.Features.Training.DTOs;

/// <summary>
/// One exported decision, as a line of the training file: what an agent decided, and what the
/// product owner made of it. Flat on purpose, because a training run reads fields rather than a
/// tree.
/// </summary>
public sealed record DecisionExportLine(
    string Id,
    string Repository,
    string Agent,
    string Category,
    string Headline,
    string Detail,
    string Evidence,
    IReadOnlyList<string> EvidenceImageIds,
    DateTimeOffset RecordedAt,
    string Verdict,
    string? Note,
    DateTimeOffset? ReviewedAt);
