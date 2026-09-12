using System.ComponentModel;
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

    public async Task<GitHeadState> GetHeadStateAsync(
        string worktreePath,
        CancellationToken cancellationToken = default)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var branch = await RunGitAsync(repoRoot, ["rev-parse", "--abbrev-ref", "HEAD"], cancellationToken);
        var listed = await RunGitAsync(repoRoot, ["branch", "--format=%(refname:short)"], cancellationToken, trimOutput: false);
        var branches = listed
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (branch.Length > 0 && !branches.Contains(branch, StringComparer.Ordinal))
            branches.Insert(0, branch);
        return new GitHeadState(branch, branches);
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
        var commitMessage = NormalizeCommitMessage(message);
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var safeFiles = files.Select(file => NormalizeRepoRelativePath(repoRoot, file)).Distinct(StringComparer.Ordinal).ToList();
        if (safeFiles.Count == 0)
            throw new InvalidOperationException("Select at least one file to commit.");

        var changed = (await GetChangesAsync(repoRoot, cancellationToken))
            .ToDictionary(change => change.Path, StringComparer.Ordinal);
        var unknown = safeFiles.FirstOrDefault(file => !changed.ContainsKey(file));
        if (unknown is not null)
            throw new InvalidOperationException($"Git file path '{unknown}' is not a changed file in this workspace.");

        var committable = new List<string>();
        foreach (var file in safeFiles)
        {
            var eligible = await CanMutatePathAsync(
                repoRoot,
                file,
                changed[file].Status,
                requireHeadForMissing: true,
                cancellationToken);
            if (eligible)
                committable.Add(file);
        }

        if (committable.Count == 0)
            throw new InvalidOperationException("The selected files are no longer in this worktree. Refresh the change list and try again.");

        var skipped = safeFiles.Where(file => !committable.Contains(file, StringComparer.Ordinal)).ToList();
        if (skipped.Count > 0)
            await RunGitAsync(repoRoot, Concat("restore", "--staged", "--", skipped), cancellationToken, allowedExitCodes: [0, 1]);

        await RunGitAsync(repoRoot, Concat("add", "--", committable), cancellationToken);
        await RunGitAsync(repoRoot, Concat("commit", "--only", "--message", commitMessage, "--", committable), cancellationToken);
        return await RunGitAsync(repoRoot, ["rev-parse", "HEAD"], cancellationToken);
    }

    public async Task DiscardAsync(
        string worktreePath,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken = default)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var safeFiles = files.Select(file => NormalizeRepoRelativePath(repoRoot, file)).Distinct(StringComparer.Ordinal).ToList();
        if (safeFiles.Count == 0)
            throw new InvalidOperationException("Select at least one file to discard.");

        var changed = (await GetChangesAsync(repoRoot, cancellationToken))
            .ToDictionary(change => change.Path, StringComparer.Ordinal);
        var unknown = safeFiles.FirstOrDefault(file => !changed.ContainsKey(file));
        if (unknown is not null)
            throw new InvalidOperationException($"Git file path '{unknown}' is not a changed file in this workspace.");

        var tracked = new List<string>();
        var untracked = new List<string>();
        foreach (var file in safeFiles)
        {
            var status = changed[file].Status;
            var onDisk = File.Exists(Path.GetFullPath(Path.Join(Path.GetFullPath(repoRoot), file)));
            var inHead = await HeadContainsAsync(repoRoot, file, cancellationToken);
            if (status is GitChangeStatus.Untracked || (status is GitChangeStatus.Added && !inHead))
            {
                if (onDisk)
                    untracked.Add(file);
                continue;
            }

            if (onDisk || inHead)
                tracked.Add(file);
        }

        if (tracked.Count == 0 && untracked.Count == 0)
            throw new InvalidOperationException("The selected files are no longer in this worktree. Refresh the change list and try again.");

        if (tracked.Count > 0)
            await RunGitAsync(repoRoot, Concat("restore", "--source=HEAD", "--staged", "--worktree", "--", tracked), cancellationToken);
        if (untracked.Count > 0)
            await RunGitAsync(repoRoot, Concat("clean", "-f", "--", untracked), cancellationToken);
    }

    public async Task SwitchBranchAsync(
        string worktreePath,
        string name,
        bool create,
        CancellationToken cancellationToken = default)
    {
        var branch = NormalizeBranchName(name);
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        if (create)
            await RunGitAsync(repoRoot, ["switch", "-c", branch], cancellationToken);
        else
            await RunGitAsync(repoRoot, ["switch", "--", branch], cancellationToken);
    }

    private static string NormalizeSeparators(string path)
        => OperatingSystem.IsWindows() ? path.Replace('\\', '/') : path;

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

    private static string NormalizeBranchName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Branch is required.");

        if (trimmed.Length > 255
            || trimmed.StartsWith('-')
            || trimmed.StartsWith('/')
            || trimmed.EndsWith('/')
            || trimmed.EndsWith(".lock", StringComparison.Ordinal)
            || trimmed.Contains("..", StringComparison.Ordinal)
            || trimmed.Contains("@{", StringComparison.Ordinal)
            || !BranchName().IsMatch(trimmed))
        {
            throw new InvalidOperationException("Branch must be a valid Git branch name.");
        }

        return trimmed;
    }

    private async Task<bool> CanMutatePathAsync(
        string repoRoot,
        string file,
        GitChangeStatus status,
        bool requireHeadForMissing,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(Path.Join(Path.GetFullPath(repoRoot), file));
        if (File.Exists(fullPath))
            return true;

        if (status == GitChangeStatus.Untracked)
            return false;

        var inHead = await HeadContainsAsync(repoRoot, file, cancellationToken);
        return requireHeadForMissing ? inHead : inHead || status is GitChangeStatus.Deleted or GitChangeStatus.Added;
    }

    private async Task<bool> HeadContainsAsync(string repoRoot, string file, CancellationToken cancellationToken)
    {
        var result = await RunGitCoreAsync(repoRoot, ["cat-file", "-e", $"HEAD:{file}"], cancellationToken);
        return result.ExitCode == 0;
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
            var isRename = code[0] is 'R' or 'C' || code[1] is 'R' or 'C';
            if (isRename && index + 1 < records.Length)
                index++;

            if (path.Length > 0)
                yield return new GitChangeEntry(NormalizeSeparators(path), MapStatus(code));
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

        var normalized = NormalizeSeparators(relative);
        if (!GitPathArgument().IsMatch(normalized))
            throw new InvalidOperationException($"Git file path '{path}' must be a safe repository-relative path.");

        return normalized;
    }

    private static List<string> Concat(string first, params object[] rest)
    {
        var arguments = new List<string> { first };
        foreach (var item in rest)
        {
            if (item is string value)
                arguments.Add(value);
            else if (item is IEnumerable<string> values)
                arguments.AddRange(values);
        }

        return arguments;
    }

    private static async Task<string> RunGitAsync(
        string worktreePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        int[]? allowedExitCodes = null,
        bool trimOutput = true)
    {
        var allowed = allowedExitCodes ?? [0];
        var result = await RunGitCoreAsync(worktreePath, arguments, cancellationToken);
        if (!allowed.Contains(result.ExitCode))
            throw new InvalidOperationException($"Git operation '{arguments[0]}' failed: {result.Stderr.Trim()}");

        return trimOutput ? result.Stdout.TrimEnd() : result.Stdout;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunGitCoreAsync(
        string worktreePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
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
        string stdout;
        string stderr;
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            stdout = await stdoutTask;
            stderr = await stderrTask;
        }
        catch (OperationCanceledException)
        {
            await KillProcessAfterCancellationAsync(process);
            throw;
        }

        return new(process.ExitCode, stdout, stderr);
    }

    private static async Task KillProcessAfterCancellationAsync(Process process)
    {
        if (process.HasExited)
            return;

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (Win32Exception)
        {
            return;
        }

        await process.WaitForExitAsync(CancellationToken.None);
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

    [GeneratedRegex("^(rev-parse|status|diff|add|commit|cat-file|restore|clean|branch|switch)$")]
    private static partial Regex AllowedGitOperation();

    [GeneratedRegex(@"^[^\u0000-\u001F\u007F]+$")]
    private static partial Regex GitPathArgument();

    [GeneratedRegex(@"^[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex BranchName();
}
