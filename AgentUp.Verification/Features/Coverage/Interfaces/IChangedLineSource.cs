using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Features.Coverage.Interfaces;

/// <summary>
/// Supplies the lines a change added or modified. Mirrors the verification module's
/// IChangedContentSource, one level finer: files there, lines here.
/// </summary>
public interface IChangedLineSource
{
    string Name { get; }

    Task<ChangedLines> GetChangedLinesAsync(string repositoryRoot, CancellationToken cancellationToken);
}
