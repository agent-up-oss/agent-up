using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Features.Verification.Services;

/// <summary>
/// Composes every registered change source and resolves what the result requires.
/// </summary>
public sealed class VerificationPlanService(
    IVerificationConfigurationLoader loader,
    CheckPlanProvider plans,
    IEnumerable<IChangedContentSource> sources)
{
    public async Task<VerificationPlan> CreatePlanAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        var configuration = loader.Load(repositoryRoot);
        var changed = await CollectChangedContentAsync(repositoryRoot, cancellationToken);
        return plans.CreatePlan(configuration, changed);
    }

    public VerificationConfiguration LoadConfiguration(string repositoryRoot)
        => loader.Load(repositoryRoot);

    /// <summary>
    /// Merges the sources. Where two sources report the same path, the first registered
    /// source wins, so working-tree content takes precedence over queued content that has
    /// since been re-applied.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> CollectChangedContentAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var contributions = new List<IReadOnlyDictionary<string, string>>();

        foreach (var source in sources)
            contributions.Add(await source.GetChangedContentHashesAsync(repositoryRoot, cancellationToken));

        return contributions
            .SelectMany(contribution => contribution)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
    }
}
