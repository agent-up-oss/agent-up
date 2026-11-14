using System.ComponentModel;
using AgentUp.Server.Features.Training.DTOs;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Training.Controllers;

/// <summary>
/// How an agent writes down a product decision it has taken.
///
/// Recording is fire and forget: it returns as soon as the decision is stored, never waits for a
/// product owner, and never tells an agent what to do next. The backlog is read by a person on
/// their own time.
/// </summary>
[McpServerToolType]
public sealed class TrainingMcpTools(TrainingController training)
{
    [McpServerTool(Name = "train_record_decision", Title = "Record a product decision")]
    [Description(
        "Write down a product decision you have taken, for the product owner to judge later. "
        + "Record and carry on: this never blocks and you will not be told the verdict. "
        + "Record a decision when you chose between real alternatives and a reasonable person could have chosen differently — "
        + "scope and priorities, what a screen says and how it behaves, where a boundary in the code goes. "
        + "Do not record what you were told to do, what only has one sensible answer, or routine work: "
        + "a backlog full of small things is a backlog nobody reads. "
        + "categoryId is one of product-vision, gui-decisions, code-architecture. "
        + "headline is the decision in one line a product owner can judge at a glance — start with the decision, not with 'I decided'. "
        + "detail is what was decided, evidence is what it was based on: what you saw, what you tried, or the decision you are following. "
        + "evidenceArtifactIds takes audit artifact ids such as the one browser_screenshot returns, so the owner sees the screen you are talking about.")]
    public async Task<McpToolResult> RecordDecision(
        [Description("The workspace you are working in. The repository is taken from it.")]
        string workspaceId,
        [Description("One of: product-vision, gui-decisions, code-architecture.")]
        string categoryId,
        [Description("The decision in one line, 12 to 120 characters, readable on its own.")]
        string headline,
        [Description("What was decided, at enough length to be acted on.")]
        string detail,
        [Description("What the call was based on: what you saw, what you tried, or the decision you are following.")]
        string evidence,
        [Description("Audit artifact ids showing the evidence, such as a browser_screenshot capture. At most four.")]
        IReadOnlyList<string>? evidenceArtifactIds = null,
        [Description("Your name, as the product owner should see it on the card.")]
        string? agent = null,
        CancellationToken cancellationToken = default)
    {
        var result = await training.RecordAsync(new RecordDecisionRequest(
            workspaceId,
            categoryId,
            headline,
            detail,
            evidence,
            evidenceArtifactIds,
            Agent: agent),
            cancellationToken: cancellationToken);

        return result.Recorded
            ? new McpToolResult(true, result.Message, result.Decision)
            : new McpToolResult(false, Refusal(result), result.Faults ?? (object?)result.Decision);
    }

    [McpServerTool(Name = "train_list_decisions", Title = "Read product decisions and their verdicts")]
    [Description(
        "Read the product decisions recorded for a repository, with whatever the product owner has made of them. "
        + "Read these before you decide something in the same area: a decision the owner marked good is how they "
        + "want this product built, and one marked bad is a mistake you should not repeat. Verdicts appear here as "
        + "soon as the owner gives them, so it is worth re-reading on a later run. "
        + "verdict narrows to good, bad or pending; category narrows to product-vision, gui-decisions or code-architecture.")]
    public Task<McpToolResult> ListDecisions(
        [Description("The workspace whose repository to read. Its repository is used when owner and repository are not given.")]
        string? workspaceId = null,
        [Description("Repository owner, when not reading from a workspace.")]
        string? owner = null,
        [Description("Repository name, when not reading from a workspace.")]
        string? repository = null,
        [Description("One of: product-vision, gui-decisions, code-architecture.")]
        string? categoryId = null,
        [Description("One of: good, bad, pending. Omit for all of them.")]
        string? verdict = null,
        [Description("How many to return, newest judged first.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var decisions = training.Read(workspaceId, owner, repository, categoryId, verdict, limit);
        return Task.FromResult(new McpToolResult(
            true,
            decisions.Count == 0
                ? "No product decisions are recorded for that repository yet."
                : $"{decisions.Count} product decision(s). A 'good' verdict is how the product owner wants this built; a 'bad' one is a call to avoid.",
            decisions));
    }

    [McpServerTool(Name = "train_get_decision", Title = "Read one product decision")]
    [Description("Read one recorded product decision in full, with the product owner's verdict and note if they have judged it.")]
    public Task<McpToolResult> GetDecision(
        [Description("The decision id, as train_list_decisions reports it.")]
        string decisionId,
        CancellationToken cancellationToken = default)
    {
        var decision = training.Find(decisionId);
        return Task.FromResult(decision is null
            ? new McpToolResult(false, $"No decision is recorded under '{decisionId}'.")
            : new McpToolResult(true, $"Decision '{decision.Headline}' is {decision.Verdict}.", decision));
    }

    [McpServerTool(Name = "train_decision_categories", Title = "Product decision categories")]
    [Description("The categories a product decision can be recorded under, with what each one covers.")]
    public Task<McpToolResult> Categories(CancellationToken cancellationToken = default)
        => Task.FromResult(new McpToolResult(
            true,
            $"Categories: {training.ExpectedCategories}.",
            training.Categories()));

    /// <summary>
    /// A refusal an agent can act on without another round trip: the duplicate it matched, or
    /// every field that needs fixing at once.
    /// </summary>
    private static string Refusal(RecordDecisionResult result)
    {
        if (result.DuplicateOfId is not null)
            return $"{result.Message} It matches decision {result.DuplicateOfId}.";

        var faults = result.Faults ?? [];
        return faults.Count == 0
            ? result.Message
            : $"{result.Message} " + string.Join(" ", faults.Select(fault => $"[{fault.Field}] {fault.Problem}"));
    }
}
