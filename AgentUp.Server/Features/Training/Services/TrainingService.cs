using AgentUp.Server.Features.Training.DTOs;
using AgentUp.Server.Features.Training.Interfaces;
using AgentUp.Server.Features.Training.Models;
using AgentUp.Server.Features.Training.Providers;

namespace AgentUp.Server.Features.Training.Services;

/// <summary>
/// The decision backlog as the surfaces and the agents meet it: categories named in words,
/// repositories counted into tiles, verdicts taken from a person, and the whole thing written out
/// as training material.
///
/// The backlog itself lives in <see cref="ProductDecisionService"/>. This is the layer that turns
/// it into and out of the shapes that travel.
/// </summary>
public sealed class TrainingService(ProductDecisionService decisions, IDecisionRepositoryResolver repositories)
{
    public string ExportMimeType => DecisionExportProvider.MimeType;

    public string ExportFileName => DecisionExportProvider.FileName;

    /// <summary>
    /// Writes down a decision an agent took, or says why it was not written down. The repository
    /// comes from the workspace rather than the caller, so a decision is filed against what the
    /// agent is actually working on.
    /// </summary>
    public RecordDecisionResult Record(RecordDecisionRequest request, DateTimeOffset now)
    {
        var category = DecisionCategoryProvider.Parse(request.Category);
        if (category is null)
            return Refused(
                $"'{request.Category}' is not a category. Use one of: {DecisionCategoryProvider.Expected}.",
                "category",
                $"Expected one of: {DecisionCategoryProvider.Expected}.");

        var repository = Repository(request);
        if (repository is null)
            return Refused(
                "No repository. Record from a workspace that has one checked out, or name owner and repository.",
                "workspaceId",
                "This workspace has no repository, and none was named.");

        return Recorded(request, category.Value, repository.Value, now);
    }

    /// <summary>Every repository with decisions, and how much of each is waiting.</summary>
    public IReadOnlyList<DecisionRepositoryDto> Repositories()
        => [.. decisions.All()
            .GroupBy(decision => decision.RepositoryKey, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count(decision => !decision.Reviewed))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(Tile)];

    /// <summary>The decisions a reviewer is about to go through.</summary>
    public IReadOnlyList<ProductDecisionDto> List(
        string? owner,
        string? repository,
        string? category,
        bool includeReviewed)
        => [.. decisions
            .List(owner, repository, DecisionCategoryProvider.Parse(category), includeReviewed)
            .Select(Dto)];

    /// <summary>Records what the product owner made of one decision.</summary>
    public ProductDecisionDto? Review(string id, ReviewDecisionRequest request, DateTimeOffset now)
    {
        var verdict = Verdict(request.Verdict);
        if (verdict is null)
            return null;

        var reviewed = decisions.Review(id, verdict.Value, request.Note, now);
        return reviewed is null ? null : Dto(reviewed);
    }

    /// <summary>The backlog as training material.</summary>
    public string Export(bool includePending) => DecisionExportProvider.Write(decisions.All(), includePending);

    /// <summary>The three categories and what each one covers, for an agent deciding where a decision goes.</summary>
    public IReadOnlyList<DecisionCategoryDto> Categories()
        => [.. DecisionCategoryProvider.All.Select(category => new DecisionCategoryDto(
            DecisionCategoryProvider.Slug(category),
            DecisionCategoryProvider.Label(category),
            DecisionCategoryProvider.Covers(category)))];

    /// <summary>What to tell an agent that named a category that does not exist.</summary>
    public string ExpectedCategories => DecisionCategoryProvider.Expected;

    private RecordDecisionResult Recorded(
        RecordDecisionRequest request,
        DecisionCategory category,
        (string Owner, string Repository) repository,
        DateTimeOffset now)
    {
        var recording = decisions.Record(
            repository.Owner,
            repository.Repository,
            request.WorkspaceId ?? string.Empty,
            string.IsNullOrWhiteSpace(request.Agent) ? "agent" : request.Agent.Trim(),
            category,
            request.Headline ?? string.Empty,
            request.Detail ?? string.Empty,
            request.Evidence ?? string.Empty,
            request.EvidenceArtifactIds,
            now);

        if (recording.Duplicate is not null)
            return new RecordDecisionResult(
                false,
                "This decision is already recorded, so it was not recorded again.",
                Dto(recording.Duplicate),
                DuplicateOfId: recording.Duplicate.Id);

        return recording.Recorded
            ? new RecordDecisionResult(true, "Recorded.", Dto(recording.Decision!))
            : new RecordDecisionResult(
                false,
                "The decision was not recorded. Fix the points below and record it again.",
                Faults: [.. recording.Faults.Select(fault => new DecisionFaultDto(fault.Field, fault.Problem))]);
    }

    private (string Owner, string Repository)? Repository(RecordDecisionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Owner) && !string.IsNullOrWhiteSpace(request.Repository))
            return (request.Owner.Trim(), request.Repository.Trim());

        return string.IsNullOrWhiteSpace(request.WorkspaceId)
            ? null
            : repositories.Resolve(request.WorkspaceId);
    }

    private static RecordDecisionResult Refused(string message, string field, string problem)
        => new(false, message, Faults: [new DecisionFaultDto(field, problem)]);

    private static DecisionRepositoryDto Tile(IGrouping<string, ProductDecision> group) => new(
        group.First().Owner,
        group.First().Repository,
        group.Count(decision => !decision.Reviewed),
        group.Count(decision => decision.Reviewed),
        [.. DecisionCategoryProvider.All.Select(category => new DecisionCategoryCountDto(
            DecisionCategoryProvider.Slug(category),
            DecisionCategoryProvider.Label(category),
            group.Count(decision => decision.Category == category && !decision.Reviewed),
            group.Count(decision => decision.Category == category && decision.Reviewed)))]);

    private static DecisionVerdict? Verdict(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "good" or "up" or "good-call" => DecisionVerdict.Good,
        "bad" or "down" or "bad-call" => DecisionVerdict.Bad,
        "pending" or "none" or "" => DecisionVerdict.Pending,
        _ => null
    };

    private static ProductDecisionDto Dto(ProductDecision decision) => new(
        decision.Id,
        decision.Owner,
        decision.Repository,
        decision.WorkspaceId,
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
