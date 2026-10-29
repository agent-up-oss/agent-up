using AgentUp.Server.Features.Training.DTOs;
using AgentUp.Server.Features.Training.Services;

namespace AgentUp.Server.Features.Training.Controllers;

/// <summary>
/// The way in to the decision backlog, for the surfaces that show it and the agents that fill it.
/// </summary>
public sealed class TrainingController(TrainingService training)
{
    public string ExportMimeType => training.ExportMimeType;

    public string ExportFileName => training.ExportFileName;

    public string ExpectedCategories => training.ExpectedCategories;

    /// <summary>Writes down a decision an agent took, or says why it was not written down.</summary>
    public RecordDecisionResult Record(RecordDecisionRequest request, DateTimeOffset? now = null)
        => training.Record(request, now ?? DateTimeOffset.UtcNow);

    /// <summary>Every repository with decisions, and how much of each is waiting.</summary>
    public IReadOnlyList<DecisionRepositoryDto> Repositories() => training.Repositories();

    /// <summary>The decisions a reviewer is about to go through.</summary>
    public IReadOnlyList<ProductDecisionDto> List(
        string? owner = null,
        string? repository = null,
        string? category = null,
        bool includeReviewed = true)
        => training.List(owner, repository, category, includeReviewed);

    /// <summary>Records what the product owner made of one decision.</summary>
    public ProductDecisionDto? Review(string id, ReviewDecisionRequest request, DateTimeOffset? now = null)
        => training.Review(id, request, now ?? DateTimeOffset.UtcNow);

    /// <summary>The backlog as training material.</summary>
    public string Export(bool includePending = false) => training.Export(includePending);

    /// <summary>The three categories and what each one covers.</summary>
    public IReadOnlyList<DecisionCategoryDto> Categories() => training.Categories();
}
