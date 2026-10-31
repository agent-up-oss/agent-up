using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>
/// The decision backlog as the Train surface reads it.
///
/// Nothing is decided here: the Server owns the backlog and every verdict, so a change is sent
/// and the answer is read back rather than kept locally and hoped for.
/// </summary>
public sealed class TrainingService(ITrainingApiProvider api)
{
    public Task<IReadOnlyList<DecisionRepository>> RepositoriesAsync(CancellationToken cancellationToken = default)
        => api.ListRepositoriesAsync(cancellationToken);

    public Task<IReadOnlyList<ProductDecision>> DecisionsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
        => api.ListDecisionsAsync(owner, repository, cancellationToken);

    public Task ReviewAsync(
        string decisionId,
        string verdict,
        string? note,
        CancellationToken cancellationToken = default)
        => api.ReviewAsync(decisionId, verdict, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), cancellationToken);

    /// <summary>
    /// One evidence picture as something the surface can draw.
    ///
    /// The bytes come over the same authenticated read as the rest of the backlog rather than from
    /// a URL the picture control would fetch on its own, which would carry no credentials and miss
    /// the Demo entirely.
    /// </summary>
    public async Task<byte[]?> EvidenceAsync(string evidenceId, CancellationToken cancellationToken = default)
    {
        var image = await api.EvidenceAsync(evidenceId, cancellationToken);
        if (image is null || string.IsNullOrWhiteSpace(image.Base64))
            return null;

        try
        {
            return Convert.FromBase64String(image.Base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public Task<string> ExportAsync(bool includePending = false, CancellationToken cancellationToken = default)
        => api.ExportAsync(includePending, cancellationToken);

    /// <summary>
    /// One file per export, named so several of them sort together in a folder of training material.
    /// </summary>
    public static string ExportFileName(DateTimeOffset now)
        => $"agent-up-product-decisions-{now:yyyy-MM-dd}.jsonl";

    /// <summary>How many decisions an export carries, which is what the reader is told afterwards.</summary>
    public static int ExportedCount(string jsonl)
        => jsonl.Split('\n').Count(line => line.Trim().Length > 0);

    /// <summary>The decisions of one category, in the order a reviewer meets them.</summary>
    public static IReadOnlyList<ProductDecision> InCategory(
        IReadOnlyList<ProductDecision> decisions,
        string category)
        => [.. decisions.Where(decision => string.Equals(decision.Category, category, StringComparison.OrdinalIgnoreCase))];

    /// <summary>How many of these nobody has judged, which is the number beside a category.</summary>
    public static int Pending(IReadOnlyList<ProductDecision> decisions)
        => decisions.Count(decision => !decision.Reviewed);

    /// <summary>
    /// Where a deck opens: the first decision nobody has judged, because that is what the reader
    /// came for. A deck with nothing waiting opens at the start.
    /// </summary>
    public static int FirstUnjudged(IReadOnlyList<ProductDecision> decisions)
    {
        var index = decisions.ToList().FindIndex(decision => !decision.Reviewed);
        return index < 0 ? 0 : index;
    }
}
