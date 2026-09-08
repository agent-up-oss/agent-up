using System.Diagnostics;
using System.Text.RegularExpressions;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Interfaces;

namespace AgentUp.Server.Features.Git.Providers;

public sealed partial class GitWorkingTreeProvider : IGitWorkingTreeProvider
{
    private const string BinaryDiffMarker = "Binary files ";

    public async Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(
        string worktreePath,
        CancellationToken cancellationToken = default)
    {
        var output = await RunGitAsync(worktreePath, ["status", "--porcelain=v1", "-z", "--untracked-files=all"], cancellationToken, trimOutput: false);
        return ParsePorcelainStatus(output)
            .DistinctBy(entry => entry.Path, StringComparer.Ordinal)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<GitFileDiff?> GetFileDiffAsync(
        string worktreePath,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var safePath = NormalizeRepoRelativePath(repoRoot, filePath);
        var changes = await GetChangesAsync(repoRoot, cancellationToken);
        var change = changes.FirstOrDefault(entry => string.Equals(entry.Path, safePath, StringComparison.Ordinal));
        if (change is null)
            return null;

        var diff = change.Status == GitChangeStatus.Untracked
            ? await RunGitAsync(repoRoot, ["diff", "--no-index", "--", NullDevicePath(), safePath], cancellationToken, allowedExitCodes: [0, 1], trimOutput: false)
            : await RunGitAsync(repoRoot, ["diff", "HEAD", "--", safePath], cancellationToken, trimOutput: false);

        return new GitFileDiff(safePath, change.Status, diff.Contains(BinaryDiffMarker, StringComparison.Ordinal), diff);
    }

    public async Task<string> CommitAsync(
        string worktreePath,
        IReadOnlyList<string> files,
        string message,
        CancellationToken cancellationToken = default)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var safeFiles = files.Select(file => NormalizeRepoRelativePath(repoRoot, file)).Distinct(StringComparer.Ordinal).ToList();
        if (safeFiles.Count == 0)
            throw new InvalidOperationException("Select at least one file to commit.");

        var commitMessage = NormalizeCommitMessage(message);

        var addArgs = new List<string> { "add", "--" };
        addArgs.AddRange(safeFiles);
        await RunGitAsync(repoRoot, addArgs, cancellationToken);

        var commitArgs = new List<string> { "commit", "--message", commitMessage, "--" };
        commitArgs.AddRange(safeFiles);
        await RunGitAsync(repoRoot, commitArgs, cancellationToken);

        return await RunGitAsync(repoRoot, ["rev-parse", "HEAD"], cancellationToken);
    }

    private static string NullDevicePath()
        => OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    private static string NormalizeCommitMessage(string message)
    {
        var trimmed = message.Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Commit message is required.");

        if (trimmed.Length > 4096 || trimmed.Contains('\0'))
            throw new InvalidOperationException("Commit message must be plain text of at most 4096 characters.");

        return trimmed;
    }

    private static IEnumerable<GitChangeEntry> ParsePorcelainStatus(string output)
    {
        var records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Length < 4)
                continue;

            var code = record[..2];
            var path = record[3..];
            // Porcelain -z emits renames as "XY NEW\0OLD\0"; the original path record is skipped.
            var isRename = code[0] is 'R' or 'C' || code[1] is 'R' or 'C';
            if (isRename && index + 1 < records.Length)
                index++;

            if (path.Length > 0)
                yield return new GitChangeEntry(path.Replace('\\', '/'), MapStatus(code));
        }
    }

    private static GitChangeStatus MapStatus(string code) => code switch
    {
        "??" => GitChangeStatus.Untracked,
        "UU" or "AA" or "DD" or "AU" or "UA" or "DU" or "UD" => GitChangeStatus.Conflicted,
        _ when code[0] is 'R' || code[1] is 'R' => GitChangeStatus.Renamed,
        _ when code[0] is 'A' || code[1] is 'A' || code[0] is 'C' || code[1] is 'C' => GitChangeStatus.Added,
        _ when code[0] is 'D' || code[1] is 'D' => GitChangeStatus.Deleted,
        _ => GitChangeStatus.Modified
    };

    private static string NormalizeRepoRelativePath(string repoRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.StartsWith(":(", StringComparison.Ordinal)
            || path.StartsWith('-')
            || path.Contains('\0')
            || path.Contains('\r')
            || path.Contains('\n'))
        {
            throw new InvalidOperationException($"Git file path '{path}' must be a literal path under the repository root.");
        }

        var normalizedRoot = Path.GetFullPath(repoRoot);
        var fullPath = Path.GetFullPath(Path.Join(normalizedRoot, path));
        var relative = Path.GetRelativePath(normalizedRoot, fullPath);
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException($"Git file path '{path}' must stay under the repository root.");

        var normalized = relative.Replace('\\', '/');
        if (!GitPathArgument().IsMatch(normalized))
            throw new InvalidOperationException($"Git file path '{path}' must be a safe repository-relative path.");

        return normalized;
    }

    private static async Task<string> RunGitAsync(
        string worktreePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        int[]? allowedExitCodes = null,
        bool trimOutput = true)
    {
        var safeWorktreePath = NormalizeWorktreePath(worktreePath);
        ValidateGitArguments(arguments);

        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(safeWorktreePath);
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        var allowed = allowedExitCodes ?? [0];
        if (!allowed.Contains(process.ExitCode))
            throw new InvalidOperationException($"Git operation '{arguments[0]}' failed: {stderr.Trim()}");

        return trimOutput ? stdout.TrimEnd() : stdout;
    }

    private static string NormalizeWorktreePath(string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath)
            || worktreePath.Contains('\0')
            || worktreePath.Contains('\r')
            || worktreePath.Contains('\n')
            || worktreePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new InvalidOperationException("Git worktree path must be an existing local directory.");
        }

        var fullPath = Path.GetFullPath(worktreePath);
        if (!Directory.Exists(fullPath))
            throw new InvalidOperationException("Git worktree path must be an existing local directory.");

        return fullPath;
    }

    private static void ValidateGitArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
            throw new InvalidOperationException("Git command must include an allowlisted operation.");

        var operation = arguments[0];
        if (!AllowedGitOperation().IsMatch(operation))
            throw new InvalidOperationException($"Git operation '{operation}' is not allowed.");

        if (arguments.Any(argument => argument.Contains('\0')))
            throw new InvalidOperationException("Git arguments must be literal values.");
    }

    [GeneratedRegex("^(rev-parse|status|diff|add|commit)$")]
    private static partial Regex AllowedGitOperation();

    [GeneratedRegex(@"^[^\u0000-\u001F\u007F]+$")]
    private static partial Regex GitPathArgument();
}
