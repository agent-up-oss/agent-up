namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>
/// A source of changed file content for one checkout. Verification composes every
/// registered source, which is what lets the commit queue contribute the content it has
/// restored out of the working tree without Verification depending on the queue.
/// </summary>
public interface IChangedContentSource
{
    /// <summary>Short name used in reports to say where a change was observed.</summary>
    string Name { get; }

    /// <summary>
    /// Repo-relative, forward-slashed path to content hash. Hashes must be produced by the
    /// same algorithm the receipt ledger records, so a source may report content that no
    /// longer exists in the working tree.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetChangedContentHashesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken);
}
