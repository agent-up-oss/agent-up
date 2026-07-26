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
            var output = await RunGitAsync(["status", "--porcelain"], cancellationToken);
            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Length > 3 ? line[3..].Trim() : "")
                .Where(path => path.Length > 0)
                .SelectMany(path => path.EndsWith('/') ? ExpandDirectory(path.TrimEnd('/')) : [path])
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    public async Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        var trackedArgs = new List<string> { "diff", "HEAD", "--" };
        trackedArgs.AddRange(files);
        var trackedDiff = await RunGitAsync(trackedArgs, cancellationToken);

        var lsArgs = new List<string> { "ls-files", "--" };
        lsArgs.AddRange(files);
        var tracked = (await RunGitAsync(lsArgs, cancellationToken))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parts = new List<string>();
        if (trackedDiff.Length > 0)
            parts.Add(trackedDiff);

        foreach (var file in files.Where(f => !tracked.Contains(f)))
        {
            var fullPath = Path.Join(workingDirectory, file);
            if (!File.Exists(fullPath))
                continue;

            var args = new List<string> { "diff", "--no-index", "--", "/dev/null", file };
            var untrackedDiff = await RunGitAsync(args, cancellationToken, allowedExitCodes: [0, 1]);
            if (untrackedDiff.Length > 0)
                parts.Add(untrackedDiff);
        }

        return string.Join('\n', parts);
    }

    public async Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunGitAsync(["diff", "--cached", "--name-only"], cancellationToken);
        return output.Length > 0;
    }

    public async Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        var lsArgs = new List<string> { "ls-files", "--" };
        lsArgs.AddRange(files);
        var tracked = (await RunGitAsync(lsArgs, cancellationToken))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toStage = files
            .Where(f => File.Exists(Path.Join(workingDirectory, f)) || tracked.Contains(f))
            .ToList();

        if (toStage.Count == 0)
            return;

        var args = new List<string> { "add", "--" };
        args.AddRange(toStage);
        await RunGitAsync(args, cancellationToken);
    }

    public Task ResetStagingAsync(CancellationToken cancellationToken = default)
        => RunGitAsync(["restore", "--staged", "."], cancellationToken, allowedExitCodes: [0, 1]);

    private IEnumerable<string> ExpandDirectory(string relativePath)
    {
        var fullPath = Path.Join(workingDirectory, relativePath);
        return Directory.Exists(fullPath)
            ? Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(workingDirectory, f).Replace('\\', '/'))
            : [];
    }

    private async Task<string> RunGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        int[]? allowedExitCodes = null)
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

        return stdout.TrimEnd();
    }
}
