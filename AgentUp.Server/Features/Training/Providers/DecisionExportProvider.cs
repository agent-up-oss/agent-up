using System.Text;
using System.Text.Json;
using AgentUp.Server.Features.Training.DTOs;
using AgentUp.Server.Features.Training.Models;

namespace AgentUp.Server.Features.Training.Providers;

/// <summary>
/// The backlog as training material: one JSON object per line, the decision an agent took beside
/// the judgement a product owner passed on it.
///
/// Line-delimited rather than one document because that is what a training run streams, and
/// because an export half-written is still readable up to its last newline.
/// </summary>
public static class DecisionExportProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string MimeType = "application/x-ndjson";

    public const string FileName = "product-decisions.jsonl";

    /// <summary>
    /// Writes the decisions out. Unreviewed ones are left out by default: a decision nobody has
    /// judged teaches nothing, and including it would train on the agent's own opinion of itself.
    /// </summary>
    public static string Write(IReadOnlyList<ProductDecision> decisions, bool includePending = false)
    {
        var builder = new StringBuilder();
        foreach (var decision in decisions.Where(decision => includePending || decision.Reviewed))
            builder.Append(JsonSerializer.Serialize(Line(decision), Json)).Append('\n');

        return builder.ToString();
    }

    private static DecisionExportLine Line(ProductDecision decision) => new(
        decision.Id,
        decision.RepositoryKey,
        decision.Agent,
        DecisionCategoryProvider.Slug(decision.Category),
        decision.Headline,
        decision.Detail,
        decision.Evidence,
        decision.EvidenceArtifactIds,
        decision.RecordedAt,
        decision.Verdict switch
        {
            DecisionVerdict.Good => "good",
            DecisionVerdict.Bad => "bad",
            _ => "pending"
        },
        decision.Note,
        decision.ReviewedAt);
}
