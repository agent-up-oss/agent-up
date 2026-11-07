using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Interfaces;

/// <summary>What the Train surface needs from the Server's decision backlog.</summary>
public interface ITrainingApiProvider
{
    Task<IReadOnlyList<DecisionRepository>> ListRepositoriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductDecision>> ListDecisionsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken);

    /// <summary>One evidence picture, or null where the backlog no longer holds it.</summary>
    Task<DecisionEvidenceImage?> EvidenceAsync(string evidenceId, CancellationToken cancellationToken);

    Task ReviewAsync(string decisionId, string verdict, string? note, CancellationToken cancellationToken);

    /// <summary>The backlog as training material, one decision per line.</summary>
    Task<string> ExportAsync(bool includePending, CancellationToken cancellationToken);
}
