namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// One check the current change set requires, together with the covered map a receipt
/// must match to satisfy it.
/// </summary>
/// <param name="Definition">The check to run.</param>
/// <param name="Covered">
/// Repo-relative path to current content hash for every changed file inside this check's
/// inputs. This is recomputed on every plan, which is what makes staleness detectable.
/// </param>
/// <param name="SkipReason">Why this check cannot run here, or None when it can.</param>
/// <param name="SelectedBy">The path rule globs that required this check, for explainability.</param>
public sealed record PlannedCheck(
    CheckDefinition Definition,
    IReadOnlyDictionary<string, string> Covered,
    CheckSkipReason SkipReason,
    IReadOnlyList<string> SelectedBy)
{
    public string CheckId => Definition.Id;

    public bool Runnable => SkipReason == CheckSkipReason.None;
}
