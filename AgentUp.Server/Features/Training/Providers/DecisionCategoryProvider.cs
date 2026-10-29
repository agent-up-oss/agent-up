using AgentUp.Server.Features.Training.Models;

namespace AgentUp.Server.Features.Training.Providers;

/// <summary>
/// The three categories as they travel and as they read.
///
/// Agents name them over MCP and clients show them, so the slug is the contract and the label is
/// presentation. Parsing is forgiving about spelling but not about meaning: an unknown category
/// is refused rather than filed under a default, because a decision in the wrong pile is worse
/// than one that was never recorded.
/// </summary>
public static class DecisionCategoryProvider
{
    public static IReadOnlyList<DecisionCategory> All =>
        [DecisionCategory.ProductVision, DecisionCategory.GuiDecisions, DecisionCategory.CodeArchitecture];

    public static string Slug(DecisionCategory category) => category switch
    {
        DecisionCategory.ProductVision => "product-vision",
        DecisionCategory.GuiDecisions => "gui-decisions",
        _ => "code-architecture"
    };

    public static string Label(DecisionCategory category) => category switch
    {
        DecisionCategory.ProductVision => "Product vision",
        DecisionCategory.GuiDecisions => "GUI decisions",
        _ => "Code architecture"
    };

    /// <summary>
    /// The category a caller named, or null when it is not one of the three. Hyphens, spaces and
    /// underscores all read the same, so an agent writing "code architecture" is understood.
    /// </summary>
    public static DecisionCategory? Parse(string? value)
    {
        var normalized = (value ?? string.Empty)
            .Trim()
            .Replace(' ', '-')
            .Replace('_', '-')
            .ToLowerInvariant();

        return normalized switch
        {
            "product-vision" or "vision" or "product" => DecisionCategory.ProductVision,
            "gui-decisions" or "gui" or "gui-decision" or "ui" or "design" => DecisionCategory.GuiDecisions,
            "code-architecture" or "architecture" or "code" => DecisionCategory.CodeArchitecture,
            _ => null
        };
    }

    /// <summary>What belongs in a category, for an agent deciding where a decision goes.</summary>
    public static string Covers(DecisionCategory category) => category switch
    {
        DecisionCategory.ProductVision =>
            "What the product is for and who for: scope, priorities, what is deliberately left out.",
        DecisionCategory.GuiDecisions =>
            "What a surface looks like and how it is used: layout, wording, flow, what a control does.",
        _ =>
            "How the code is shaped: boundaries, data flow, dependencies, what owns what."
    };

    /// <summary>What to tell an agent that named something else.</summary>
    public static string Expected =>
        string.Join(", ", All.Select(Slug));
}
