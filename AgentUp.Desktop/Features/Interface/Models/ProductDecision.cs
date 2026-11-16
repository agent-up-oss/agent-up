namespace AgentUp.Desktop.Features.Interface.Models;

/// <summary>A picture behind a decision, kept by the backlog rather than by the run that took it.</summary>
public sealed record DecisionEvidence(string EvidenceId, string SourceArtifactId, string MimeType);

/// <summary>An evidence picture's bytes, as the Server hands them over.</summary>
public sealed record DecisionEvidenceImage(string EvidenceId, string MimeType, string Base64);

/// <summary>One decision an agent took, and whatever the product owner has made of it.</summary>
public sealed record ProductDecision(
    string Id,
    string Owner,
    string Repository,
    string WorkspaceId,
    string Agent,
    string Category,
    string Headline,
    string Detail,
    string Evidence,
    IReadOnlyList<DecisionEvidence> EvidenceImages,
    DateTimeOffset RecordedAt,
    string Verdict,
    string? Note,
    DateTimeOffset? ReviewedAt)
{
    /// <summary>True once the product owner has judged it, whichever way they went.</summary>
    public bool Reviewed => !string.Equals(Verdict, "pending", StringComparison.OrdinalIgnoreCase);

    public string RepositoryKey => $"{Owner}/{Repository}";
}

/// <summary>How much of one category of one repository is waiting.</summary>
public sealed record DecisionCategoryCount(string Category, string Label, int Pending, int Reviewed);

/// <summary>One repository's tile on the Train surface.</summary>
public sealed record DecisionRepository(
    string Owner,
    string Repository,
    int Pending,
    int Reviewed,
    IReadOnlyList<DecisionCategoryCount> Categories)
{
    public string Key => $"{Owner}/{Repository}";
}
