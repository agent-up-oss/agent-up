using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;

namespace AgentUp.Desktop.Features.Interface.Controllers;

/// <summary>The way in to the decision backlog for the surfaces that show it.</summary>
public sealed class TrainingController(TrainingService training)
{
    public Task<IReadOnlyList<DecisionRepository>> RepositoriesAsync(CancellationToken cancellationToken = default)
        => training.RepositoriesAsync(cancellationToken);

    public Task<IReadOnlyList<ProductDecision>> DecisionsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
        => training.DecisionsAsync(owner, repository, cancellationToken);

    public Task ReviewAsync(
        string decisionId,
        string verdict,
        string? note,
        CancellationToken cancellationToken = default)
        => training.ReviewAsync(decisionId, verdict, note, cancellationToken);

    public Task<byte[]?> EvidenceAsync(string evidenceId, CancellationToken cancellationToken = default)
        => training.EvidenceAsync(evidenceId, cancellationToken);

    public Task<string> ExportAsync(bool includePending = false, CancellationToken cancellationToken = default)
        => training.ExportAsync(includePending, cancellationToken);
}
