using System.Diagnostics;
using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Features.Coverage.Providers;

/// <summary>
/// Derives changed lines from Git: everything this branch has added or modified relative
/// to its base, plus anything still uncommitted, plus whole untracked files.
/// </summary>
public sealed class GitChangedLineSource(UnifiedDiffParser parser) : IChangedLineSource
{
    public string Name => "git";

    public async Task<ChangedLines> GetChangedLinesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var worktree = parser.Parse(await ReadOutputAsync(
            repositoryRoot, ["diff", "--unified=0", "--no-color", "--no-renames", "HEAD"], cancellationToken));

        var committed = await ReadCommittedAsync(repositoryRoot, cancellationToken);
        var untracked = await ReadUntrackedAsync(repositoryRoot, cancellationToken);

        return worktree.MergeWith(committed).MergeWith(untracked);
    }

    private async Task<ChangedLines> ReadCommittedAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var baseRef = await ResolveBaseRefAsync(repositoryRoot, cancellationToken);
        if (baseRef is null)
            return ChangedLines.None;

        return parser.Parse(await ReadOutputAsync(
            repositoryRoot,
            ["diff", "--unified=0", "--no-color", "--no-renames", baseRef + "...HEAD"],
            cancellationToken));
    }

    /// <summary>
    /// An untracked file has no diff, so every line in it counts as added. Without this a
    /// brand-new production file would contribute nothing and score as fully covered.
    /// </summary>
    private static async Task<ChangedLines> ReadUntrackedAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var output = await ReadOutputAsync(
            repositoryRoot, ["ls-files", "--others", "--exclude-standard", "-z"], cancellationToken);

        var paths = output
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(PathGlobProvider.Normalize)
            .Where(path => path.Length > 0);

        var pairs = new List<KeyValuePair<string, IReadOnlySet<int>>>();

        foreach (var path in paths)
        {
            var absolute = Path.Join(repositoryRoot, path);
            if (!File.Exists(absolute))
                continue;

            var lineCount = (await File.ReadAllLinesAsync(absolute, cancellationToken)).Length;
            if (lineCount == 0)
                continue;

            pairs.Add(new KeyValuePair<string, IReadOnlySet<int>>(
                path, Enumerable.Range(1, lineCount).ToHashSet()));
        }

        return ChangedLines.FromPairs(pairs);
    }

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
