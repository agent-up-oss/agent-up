using AgentUp.Server.Features.Training.Models;

namespace AgentUp.Server.Features.Training.Providers;

/// <summary>What a draft decision was rejected for, in words an agent can act on.</summary>
public sealed record DecisionDraftFault(string Field, string Problem);

/// <summary>
/// Whether a decision an agent wrote is worth a product owner's attention, judged on its shape.
///
/// A reviewer goes through these one card at a time on a phone, so the cost of a vague entry is
/// paid by a person rather than by the agent that wrote it. None of this can tell whether a
/// decision mattered — only the agent knows that, and the instructions ask it to be selective —
/// but it can refuse the entries that are unreadable on their face.
/// </summary>
public static class DecisionDraftProvider
{
    /// <summary>A headline has to fit a card and read as one thought.</summary>
    public const int HeadlineMinimum = 12;

    public const int HeadlineMaximum = 120;

    /// <summary>Below this a detail is a restatement of the headline rather than the decision.</summary>
    public const int DetailMinimum = 30;

    public const int DetailMaximum = 2000;

    /// <summary>Below this an evidence line says "it seemed right", which teaches a model nothing.</summary>
    public const int EvidenceMinimum = 20;

    public const int EvidenceMaximum = 2000;

    /// <summary>More pictures than a reviewer will look at on one card.</summary>
    public const int EvidenceImageMaximum = 4;

    /// <summary>
    /// Openings that carry no information, so the one line a reviewer reads starts with the
    /// decision instead of the fact that a decision was taken.
    /// </summary>
    private static readonly string[] EmptyOpenings =
    [
        "i decided", "we decided", "decided to", "decided that", "the decision",
        "this decision", "made a decision", "chose to", "i chose", "it was decided"
    ];

    /// <summary>
    /// Everything wrong with a draft, so an agent fixes it in one go rather than one round trip
    /// per field.
    /// </summary>
    public static IReadOnlyList<DecisionDraftFault> Faults(
        string? headline,
        string? detail,
        string? evidence,
        IReadOnlyList<string>? evidenceArtifactIds)
    {
        var faults = new List<DecisionDraftFault>();
        faults.AddRange(HeadlineFaults(headline));

        var trimmedDetail = detail?.Trim() ?? string.Empty;
        if (trimmedDetail.Length < DetailMinimum)
            faults.Add(new DecisionDraftFault(
                "detail",
                $"Say what was decided in at least {DetailMinimum} characters. The headline is the claim; this is the substance behind it."));
        else if (trimmedDetail.Length > DetailMaximum)
            faults.Add(new DecisionDraftFault("detail", $"Keep the detail under {DetailMaximum} characters."));

        var trimmedEvidence = evidence?.Trim() ?? string.Empty;
        if (trimmedEvidence.Length < EvidenceMinimum)
            faults.Add(new DecisionDraftFault(
                "evidence",
                $"Say what the call was based on in at least {EvidenceMinimum} characters: what you saw, what you tried, or the decision you are following."));
        else if (trimmedEvidence.Length > EvidenceMaximum)
            faults.Add(new DecisionDraftFault("evidence", $"Keep the evidence under {EvidenceMaximum} characters."));

        if (evidenceArtifactIds is { Count: > EvidenceImageMaximum })
            faults.Add(new DecisionDraftFault(
                "evidenceArtifactIds",
                $"Attach at most {EvidenceImageMaximum} images. Pick the ones that show the decision."));

        if (evidenceArtifactIds is not null && evidenceArtifactIds.Any(string.IsNullOrWhiteSpace))
            faults.Add(new DecisionDraftFault(
                "evidenceArtifactIds",
                "An artifact id is empty. Pass the ids browser_screenshot returned, not file paths."));

        return faults;
    }

    private static IEnumerable<DecisionDraftFault> HeadlineFaults(string? headline)
    {
        var trimmed = headline?.Trim() ?? string.Empty;
        if (trimmed.Length < HeadlineMinimum)
        {
            yield return new DecisionDraftFault(
                "headline",
                $"Write the decision as one line of at least {HeadlineMinimum} characters, readable on its own.");
            yield break;
        }

        if (trimmed.Length > HeadlineMaximum)
            yield return new DecisionDraftFault(
                "headline",
                $"Keep the headline under {HeadlineMaximum} characters so it fits a card. Put the rest in the detail.");

        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
            yield return new DecisionDraftFault("headline", "Keep the headline to one line.");

        var opening = EmptyOpenings.FirstOrDefault(
            start => trimmed.StartsWith(start, StringComparison.OrdinalIgnoreCase));
        if (opening is not null)
            yield return new DecisionDraftFault(
                "headline",
                $"Start with the decision itself rather than \"{opening}\". A reviewer already knows it is a decision.");
    }

    /// <summary>
    /// Whether two decisions are the same decision. Agents work in loops and a decision reached
    /// twice is still one decision, so the second recording is dropped rather than queued for a
    /// person to dismiss.
    /// </summary>
    public static bool SameDecision(ProductDecision existing, string owner, string repository, DecisionCategory category, string headline)
        => string.Equals(existing.Owner, owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && existing.Category == category
            && string.Equals(Normalize(existing.Headline), Normalize(headline), StringComparison.OrdinalIgnoreCase);

    /// <summary>Compares headlines by their words, so punctuation and spacing do not hide a repeat.</summary>
    private static string Normalize(string headline)
        => string.Join(' ', headline.Split(
            [' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '"', '\'', '-', '—'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
