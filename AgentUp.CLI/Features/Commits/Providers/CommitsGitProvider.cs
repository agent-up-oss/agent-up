using System.Diagnostics;
using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Providers;

public sealed class CommitsGitProvider(string workingDirectory) : ICommitsGitProvider
{
    public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
        => RunGitAsync(["rev-parse", "--show-toplevel"], cancellationToken);

    public async Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var repoRoot = await GetRepoRootAsync(cancellationToken);
            var output = await RunGitAsync(["status", "--porcelain"], cancellationToken);
            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Length > 3 ? line[3..].Trim() : "")
                .Where(path => path.Length > 0)
                .SelectMany(path => path.EndsWith('/') ? ExpandDirectory(path.TrimEnd('/'), repoRoot) : [path])
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<string>> GetStagedFilesAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunGitAsync(["diff", "--cached", "--name-only"], cancellationToken);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public async Task<IReadOnlyList<string>> GetUntrackedFilesAsync(CancellationToken cancellationToken = default)
    {
        var repoRoot = await GetRepoRootAsync(cancellationToken);
        var output = await RunGitAsync(["ls-files", "--others", "--exclude-standard"], cancellationToken);
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => NormalizeRepoRelativePath(repoRoot, path))
            .ToList();
    }

    public async Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        var repoRoot = await GetRepoRootAsync(cancellationToken);
        var safeFiles = files.Select(f => NormalizeRepoRelativePath(repoRoot, f)).ToList();
        var trackedArgs = new List<string> { "diff", "--binary", "--full-index", "HEAD", "--" };
        trackedArgs.AddRange(safeFiles);
        var trackedDiff = await RunGitAsync(trackedArgs, cancellationToken, trimOutput: false);

        var lsArgs = new List<string> { "ls-files", "--" };
        lsArgs.AddRange(safeFiles);
        var tracked = (await RunGitAsync(lsArgs, cancellationToken))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parts = new List<string>();
        if (trackedDiff.Length > 0)
            parts.Add(trackedDiff);

        foreach (var file in safeFiles.Where(f => !tracked.Contains(f) && File.Exists(Path.Join(repoRoot, f))))
        {
            var args = new List<string> { "diff", "--binary", "--full-index", "--no-index", "--", "/dev/null", file };
            var untrackedDiff = await RunGitAsync(args, cancellationToken, allowedExitCodes: [0, 1], trimOutput: false);
            if (untrackedDiff.Length > 0)
                parts.Add(untrackedDiff);
        }

        return string.Join('\n', parts);
    }

    public async Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
        => (await GetStagedFilesAsync(cancellationToken)).Count > 0;

    public async Task ApplyPatchAsync(string patch, CancellationToken cancellationToken = default)
    {
        if (patch.Length == 0)
            return;

        var tempPath = Path.GetTempFileName();
        try
        {
            var content = patch.EndsWith('\n') ? patch : patch + "\n";
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);
            await RunGitAsync(["apply", tempPath], cancellationToken);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    public async Task RestoreFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        var repoRoot = await GetRepoRootAsync(cancellationToken);
        var safeFiles = files.Select(f => NormalizeRepoRelativePath(repoRoot, f)).ToList();
        var lsArgs = new List<string> { "ls-files", "--" };
        lsArgs.AddRange(safeFiles);
        var tracked = (await RunGitAsync(lsArgs, cancellationToken))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toRestore = safeFiles.Where(f => tracked.Contains(f)).ToList();
        if (toRestore.Count > 0)
        {
            var args = new List<string> { "restore", "--" };
            args.AddRange(toRestore);
            await RunGitAsync(args, cancellationToken);
        }

        foreach (var file in safeFiles.Where(f => !tracked.Contains(f) && File.Exists(Path.Join(repoRoot, f))))
            File.Delete(Path.Join(repoRoot, file));
    }

    public async Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        var repoRoot = await GetRepoRootAsync(cancellationToken);
        var safeFiles = files.Select(f => NormalizeRepoRelativePath(repoRoot, f)).ToList();
        var lsArgs = new List<string> { "ls-files", "--" };
        lsArgs.AddRange(safeFiles);
        var tracked = (await RunGitAsync(lsArgs, cancellationToken))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toStage = safeFiles
            .Where(f => File.Exists(Path.Join(repoRoot, f)) || tracked.Contains(f))
            .ToList();

        if (toStage.Count == 0)
            return;

        var args = new List<string> { "add", "--" };
        args.AddRange(toStage);
        await RunGitAsync(args, cancellationToken);
    }

    public Task ResetStagingAsync(CancellationToken cancellationToken = default)
        => RunGitAsync(["restore", "--staged", "."], cancellationToken, allowedExitCodes: [0, 1]);

    private static IEnumerable<string> ExpandDirectory(string relativePath, string repoRoot)
    {
        var fullPath = Path.Join(repoRoot, relativePath);
        return Directory.Exists(fullPath)
            ? Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/'))
            : [];
    }

    private static string NormalizeRepoRelativePath(string repoRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.StartsWith(":(", StringComparison.Ordinal))
            throw new InvalidOperationException($"Commit queue file path '{path}' must be a literal path under the repository root.");

        var normalizedRoot = Path.GetFullPath(repoRoot);
        var fullPath = Path.GetFullPath(Path.Join(normalizedRoot, path));
        var relative = Path.GetRelativePath(normalizedRoot, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException($"Commit queue file path '{path}' must stay under the repository root.");

        return relative.Replace('\\', '/');
    }

    private async Task<string> RunGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        int[]? allowedExitCodes = null,
        bool trimOutput = true)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var allowed = allowedExitCodes ?? [0];
        if (!allowed.Contains(process.ExitCode))
            throw new InvalidOperationException($"git {string.Join(" ", arguments)} failed: {stderr.Trim()}");

        return trimOutput ? stdout.TrimEnd() : stdout;
    }
}
