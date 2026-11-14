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
public sealed class TrainingService(
    ProductDecisionService decisions,
    IDecisionRepositoryResolver repositories,
    IDecisionEvidenceStore evidence,
    IDecisionEvidenceSource captures)
{
    public string ExportMimeType => DecisionExportProvider.MimeType;

    public string ExportFileName => DecisionExportProvider.FileName;

    /// <summary>
    /// Writes down a decision an agent took, or says why it was not written down. The repository
    /// comes from the workspace rather than the caller, so a decision is filed against what the
    /// agent is actually working on.
    /// </summary>
    public async Task<RecordDecisionResult> RecordAsync(
        RecordDecisionRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
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

        var faults = DecisionDraftProvider.Faults(
            request.Headline,
            request.Detail,
            request.Evidence,
            request.EvidenceArtifactIds);
        if (faults.Count > 0)
            return Refused([.. faults.Select(fault => new DecisionFaultDto(fault.Field, fault.Problem))]);

        var images = await CopyEvidenceAsync(request.EvidenceArtifactIds, cancellationToken);
        return images.Missing.Count > 0
            ? Refused([new DecisionFaultDto(
                "evidenceArtifactIds",
                $"No capture is stored under {string.Join(", ", images.Missing)}. Pass the artifact id browser_screenshot returned for this run.")])
            : Recorded(request, category.Value, repository.Value, images.Images, now);
    }

    /// <summary>
    /// Takes the backlog's own copy of every picture before the decision is written down, so a
    /// recorded decision never points at something that can go away.
    /// </summary>
    private async Task<(List<DecisionEvidenceImage> Images, List<string> Missing)> CopyEvidenceAsync(
        IReadOnlyList<string>? artifactIds,
        CancellationToken cancellationToken)
    {
        var images = new List<DecisionEvidenceImage>();
        var missing = new List<string>();
        foreach (var artifactId in artifactIds ?? [])
        {
            var capture = await captures.ReadAsync(artifactId, cancellationToken);
            if (capture is null)
                missing.Add(artifactId);
            else
                images.Add(await evidence.SaveAsync(artifactId, capture.Value.MimeType, capture.Value.Bytes, cancellationToken));
        }

        return (images, missing);
    }

    /// <summary>The bytes behind one evidence picture, for a surface that shows it.</summary>
    public async Task<(string MimeType, byte[] Bytes)?> EvidenceAsync(string evidenceId, CancellationToken cancellationToken = default)
    {
        var stored = await evidence.LoadAsync(evidenceId, cancellationToken);
        return stored is null ? null : (stored.Value.Image.MimeType, stored.Value.Bytes);
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

    /// <summary>
    /// What an agent reads back. A workspace names its own repository, so an agent asks about the
    /// product it is working on without having to know what that product is called. Judged
    /// decisions come first: they are the ones that carry an instruction.
    /// </summary>
    public IReadOnlyList<ProductDecisionDto> Read(
        string? workspaceId,
        string? owner,
        string? repository,
        string? category,
        string? verdict,
        int limit)
    {
        var resolved = Named(owner, repository) ?? Workspace(workspaceId);
        var wanted = WantedVerdict(verdict);
        return [.. decisions
            .List(resolved?.Owner, resolved?.Repository, DecisionCategoryProvider.Parse(category), includeReviewed: true)
            .Where(decision => wanted is null || decision.Verdict == wanted)
            .OrderByDescending(decision => decision.ReviewedAt ?? decision.RecordedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(Dto)];
    }

    /// <summary>One recorded decision, whoever is asking.</summary>
    public ProductDecisionDto? Find(string id)
    {
        var decision = decisions.All().FirstOrDefault(item => item.Id == id);
        return decision is null ? null : Dto(decision);
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
        IReadOnlyList<DecisionEvidenceImage> images,
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
            images,
            now);

        if (recording.Duplicate is not null)
            return new RecordDecisionResult(
                false,
                "This decision is already recorded, so it was not recorded again.",
                Dto(recording.Duplicate),
                DuplicateOfId: recording.Duplicate.Id);

        return new RecordDecisionResult(true, "Recorded.", Dto(recording.Decision!));
    }

    private (string Owner, string Repository)? Repository(RecordDecisionRequest request)
        => Named(request.Owner, request.Repository) ?? Workspace(request.WorkspaceId);

    private static (string Owner, string Repository)? Named(string? owner, string? repository)
        => string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository)
            ? null
            : (owner.Trim(), repository.Trim());

    private (string Owner, string Repository)? Workspace(string? workspaceId)
        => string.IsNullOrWhiteSpace(workspaceId) ? null : repositories.Resolve(workspaceId);

    private static RecordDecisionResult Refused(string message, string field, string problem)
        => new(false, message, Faults: [new DecisionFaultDto(field, problem)]);

    private static RecordDecisionResult Refused(IReadOnlyList<DecisionFaultDto> faults)
        => new(false, "The decision was not recorded. Fix the points below and record it again.", Faults: faults);

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

    /// <summary>
    /// The verdict a reader is narrowing to, or null for all of them. Naming none is not the same
    /// as naming "pending": an agent asking what has been decided wants the judged ones most.
    /// </summary>
    private static DecisionVerdict? WantedVerdict(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Verdict(value);

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
        [.. decision.EvidenceImages.Select(image => new DecisionEvidenceDto(image.EvidenceId, image.SourceArtifactId, image.MimeType))],
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
