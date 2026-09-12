using System.Diagnostics;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// The always-on change source: everything this branch has touched that is not yet on the
/// base branch, plus anything still uncommitted.
/// </summary>
/// <remarks>
/// Reading Git directly rather than the commit queue is what makes the commit module
/// optional. A repository with no queue, no commits configuration and no commits MCP
/// server still gets the full gate.
/// </remarks>
public sealed class GitChangedContentSource(ContentHashProvider hashes, GitChangeOutputParser parser) : IChangedContentSource
{
    public string Name => "git";

    public async Task<IReadOnlyDictionary<string, string>> GetChangedContentHashesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        // --untracked-files=all matters: by default Git collapses a wholly untracked
        // directory into one entry with a trailing slash, which would hide every new file
        // inside it from the covered map.
        var status = await ReadOutputAsync(
            repositoryRoot,
            ["status", "--porcelain=v1", "-z", "--no-renames", "--untracked-files=all"],
            cancellationToken);
        var committed = await ReadCommittedPathsAsync(repositoryRoot, cancellationToken);

        var paths = parser.ParseStatus(status)
            .Concat(committed)
            .Select(PathGlobProvider.Normalize)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        return paths.ToDictionary(
            path => path,
            path => hashes.HashFile(Path.Join(repositoryRoot, path)),
            StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<string>> ReadCommittedPathsAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var baseRef = await ResolveBaseRefAsync(repositoryRoot, cancellationToken);
        if (baseRef is null)
            return [];

        var diff = await ReadOutputAsync(
            repositoryRoot, ["diff", "--name-only", "-z", "--no-renames", baseRef + "...HEAD"], cancellationToken);
        return parser.ParseNames(diff);
    }

    /// <summary>
    /// Finds the merge base with the first default-branch candidate that exists. Returns
    /// null on a repository with no such branch, in which case only the working tree is
    /// considered rather than treating the whole history as changed.
    /// </summary>
    private static async Task<string?> ResolveBaseRefAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        string[] candidates = ["origin/main", "origin/master", "main", "master"];

        foreach (var candidate in candidates)
        {
            var mergeBase = await ReadOutputAsync(
                repositoryRoot, ["merge-base", candidate, "HEAD"], cancellationToken);
            var resolved = mergeBase.Trim();
            if (resolved.Length > 0)
                return resolved;
        }

        return null;
    }

    private static async Task<string> ReadOutputAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        if (process is null)
            return string.Empty;

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode == 0 ? output : string.Empty;
    }
}
