namespace AgentUp.Server.Features.Training.Models;

/// <summary>
/// What part of the product a decision is about. A reviewer reads one of these at a time, so an
/// agent that cannot say which of the three a decision belongs to has usually made two decisions.
/// </summary>
public enum DecisionCategory
{
    /// <summary>What the product is for, and who it is for: scope, priorities, what is left out.</summary>
    ProductVision,

    /// <summary>What the thing looks like and how it is used: layout, wording, flow, affordances.</summary>
    GuiDecisions,

    /// <summary>How the code is shaped: boundaries, data flow, dependencies, what owns what.</summary>
    CodeArchitecture
}

/// <summary>What the product owner said about a decision, once they have looked at it.</summary>
public enum DecisionVerdict
{
    /// <summary>Nobody has judged it yet. Everything starts here.</summary>
    Pending,

    /// <summary>The right call.</summary>
    Good,

    /// <summary>The wrong call.</summary>
    Bad
}

/// <summary>
/// One product decision an agent took while working, and what the product owner made of it.
///
/// This is a record of something already done, not a request: agents write these and carry on, and
/// nothing an agent does waits on a verdict. The verdict exists so the decision and the judgement
/// can be read back together later, as the material a model is trained on.
/// </summary>
/// <param name="Headline">
/// The decision itself in one line, which is the only part a reviewer is guaranteed to read.
/// </param>
/// <param name="Detail">What was decided, at enough length to be acted on.</param>
/// <param name="Evidence">What the decision was based on: what was seen, tried, or already settled.</param>
/// <param name="EvidenceArtifactIds">
/// Audit artifacts that show the evidence, such as the screenshot <c>browser_screenshot</c>
/// stored. The image lives in the audit store once; this points at it rather than copying it.
/// </param>
public sealed record ProductDecision(
    string Id,
    string Owner,
    string Repository,
    string WorkspaceId,
    string Agent,
    DecisionCategory Category,
    string Headline,
    string Detail,
    string Evidence,
    IReadOnlyList<string> EvidenceArtifactIds,
    DateTimeOffset RecordedAt,
    DecisionVerdict Verdict = DecisionVerdict.Pending,
    string? Note = null,
    DateTimeOffset? ReviewedAt = null)
{
    /// <summary>True once the product owner has judged it, whichever way they went.</summary>
    public bool Reviewed => Verdict != DecisionVerdict.Pending;

    /// <summary>The repository this belongs to, as the Train tab tiles them.</summary>
    public string RepositoryKey => $"{Owner}/{Repository}";
}
