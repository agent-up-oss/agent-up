using AgentUp.Server.Features.Training.Interfaces;
using AgentUp.Server.Features.Training.Models;
using AgentUp.Server.Features.Training.Providers;

namespace AgentUp.Server.Features.Training.Services;

/// <summary>Why a recording was refused, or the decision it produced.</summary>
public sealed record DecisionRecording(
    ProductDecision? Decision,
    IReadOnlyList<DecisionDraftFault> Faults,
    ProductDecision? Duplicate)
{
    public bool Recorded => Decision is not null && Faults.Count == 0 && Duplicate is null;
}

/// <summary>
/// The backlog of product decisions the agents have taken, and what the product owner made of
/// them.
///
/// Recording is the hot path and nothing waits on it: an agent writes a decision mid-run and
/// carries on, so this never blocks on a review and never calls back out to an agent. Reviewing
/// is the slow path, done by a person later, and the two meet only in the export.
/// </summary>
public sealed class ProductDecisionService
{
    private readonly IProductDecisionStore _store;
    private readonly List<ProductDecision> _decisions;
    private readonly object _gate = new();

    public ProductDecisionService(IProductDecisionStore store)
    {
        _store = store;
        _decisions = [.. store.Read()];
    }

    /// <summary>
    /// Writes one decision down, or says why it was not worth writing down. A draft that is
    /// unreadable on its face is refused with the reasons; a decision already recorded for this
    /// repository and category is dropped rather than queued twice for the same person.
    /// </summary>
    public DecisionRecording Record(
        string owner,
        string repository,
        string workspaceId,
        string agent,
        DecisionCategory category,
        string headline,
        string detail,
        string evidence,
        IReadOnlyList<DecisionEvidenceImage> evidenceImages,
        DateTimeOffset recordedAt)
    {

        lock (_gate)
        {
            var duplicate = _decisions.FirstOrDefault(
                existing => DecisionDraftProvider.SameDecision(existing, owner, repository, category, headline));
            if (duplicate is not null)
                return new DecisionRecording(null, [], duplicate);

            var decision = new ProductDecision(
                Guid.NewGuid().ToString("n"),
                owner,
                repository,
                workspaceId,
                agent,
                category,
                headline.Trim(),
                detail.Trim(),
                evidence.Trim(),
                evidenceImages,
                recordedAt);

            _decisions.Add(decision);
            _store.Write(_decisions);
            return new DecisionRecording(decision, [], null);
        }
    }

    /// <summary>
    /// The decisions in one repository and category, newest last so a reviewer works forwards
    /// through them. Unreviewed ones come first: they are the reason to open the tab.
    /// </summary>
    public IReadOnlyList<ProductDecision> List(
        string? owner = null,
        string? repository = null,
        DecisionCategory? category = null,
        bool includeReviewed = true)
    {
        lock (_gate)
        {
            return _decisions
                .Where(decision => owner is null || string.Equals(decision.Owner, owner, StringComparison.OrdinalIgnoreCase))
                .Where(decision => repository is null || string.Equals(decision.Repository, repository, StringComparison.OrdinalIgnoreCase))
                .Where(decision => category is null || decision.Category == category)
                .Where(decision => includeReviewed || !decision.Reviewed)
                .OrderBy(decision => decision.Reviewed)
                .ThenBy(decision => decision.RecordedAt)
                .ToList();
        }
    }

    /// <summary>Everything recorded, in the order it was recorded.</summary>
    public IReadOnlyList<ProductDecision> All()
    {
        lock (_gate)
            return [.. _decisions.OrderBy(decision => decision.RecordedAt)];
    }

    /// <summary>
    /// Records what the product owner made of one decision. Judging the same decision again
    /// replaces the verdict rather than adding one, because a person is allowed to change their
    /// mind and the export carries what they think now.
    /// </summary>
    public ProductDecision? Review(string id, DecisionVerdict verdict, string? note, DateTimeOffset reviewedAt)
    {
        lock (_gate)
        {
            var index = _decisions.FindIndex(decision => decision.Id == id);
            if (index < 0)
                return null;

            var reviewed = _decisions[index] with
            {
                Verdict = verdict,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                ReviewedAt = verdict == DecisionVerdict.Pending ? null : reviewedAt
            };

            _decisions[index] = reviewed;
            _store.Write(_decisions);
            return reviewed;
        }
    }
}
