using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Tests.Fake;

/// <summary>
/// A change source with a fixed answer, standing in for Git or the commit queue.
/// </summary>
internal sealed class StaticChangedContentSource(
    string name,
    IReadOnlyDictionary<string, string> changed) : IChangedContentSource
{
    public string Name => name;

    public Task<IReadOnlyDictionary<string, string>> GetChangedContentHashesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
        => Task.FromResult(changed);
}
