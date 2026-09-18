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
        var commit = await RunGitAsync(repoRoot, ["rev-parse", "HEAD"], cancellationToken);
        var listed = await RunGitAsync(repoRoot, ["branch", "--format=%(refname:short)"], cancellationToken, trimOutput: false);
        var remoteListed = await RunGitAsync(
            repoRoot,
            ["branch", "-r", "--format=%(refname:short)"],
            cancellationToken,
            trimOutput: false);
        var branches = ParseLines(listed)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (branch.Length > 0 && !branches.Contains(branch, StringComparer.Ordinal))
            branches.Insert(0, branch);

        var remotes = ParseRemoteBranches(remoteListed);
        var (upstream, ahead, behind) = await ReadUpstreamAsync(repoRoot, cancellationToken);
        return new GitHeadState(branch, branches, remotes, upstream, ahead, behind, commit);
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
        var stagedAdditions = new List<string>();
        foreach (var file in safeFiles)
        {
            var status = changed[file].Status;
            var onDisk = File.Exists(Path.GetFullPath(Path.Join(Path.GetFullPath(repoRoot), file)));
            var inHead = await HeadContainsAsync(repoRoot, file, cancellationToken);
            if (status is GitChangeStatus.Added && !inHead)
            {
                stagedAdditions.Add(file);
                continue;
            }

            if (status is GitChangeStatus.Untracked)
            {
                if (onDisk)
                    untracked.Add(file);
                continue;
            }

            if (onDisk || inHead)
                tracked.Add(file);
        }

        if (tracked.Count == 0 && untracked.Count == 0 && stagedAdditions.Count == 0)
            throw new InvalidOperationException("The selected files are no longer in this worktree. Refresh the change list and try again.");

        if (tracked.Count > 0)
            await RunGitAsync(repoRoot, Concat("restore", "--source=HEAD", "--staged", "--worktree", "--", tracked), cancellationToken);
        if (stagedAdditions.Count > 0)
        {
            await RunGitAsync(repoRoot, Concat("restore", "--staged", "--", stagedAdditions), cancellationToken);
            untracked.AddRange(stagedAdditions.Where(file =>
                File.Exists(Path.GetFullPath(Path.Join(Path.GetFullPath(repoRoot), file)))));
        }
        if (untracked.Count > 0)
            await RunGitAsync(repoRoot, Concat("clean", "-f", "--", untracked), cancellationToken);
    }

    public async Task SwitchBranchAsync(
        string worktreePath,
        string name,
        bool create,
        CancellationToken cancellationToken = default)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var branch = await NormalizeBranchNameAsync(repoRoot, name, cancellationToken);
        if (create)
            await RunGitAsync(repoRoot, ["switch", "-c", branch], cancellationToken);
        else
            await RunGitAsync(repoRoot, ["switch", "--", branch], cancellationToken);
    }

    public async Task CheckoutRemoteAsync(
        string worktreePath,
        string name,
        CancellationToken cancellationToken = default)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var requested = await NormalizeBranchNameAsync(repoRoot, name, cancellationToken);
        var head = await GetHeadStateAsync(repoRoot, cancellationToken);
        var matches = MatchRemoteBranches(requested, head.RemoteBranches);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No remote-tracking branch named '{requested}'.");
        if (matches.Count > 1)
            throw new InvalidOperationException($"Branch '{requested}' exists on multiple remotes.");

        var target = matches[0];
        var localName = target.Name;
        if (head.LocalBranches.Contains(localName, StringComparer.Ordinal))
        {
            await RunGitAsync(repoRoot, ["switch", "--", localName], cancellationToken);
            return;
        }

        var startPoint = $"{target.Remote}/{target.Name}";
        await RunGitAsync(repoRoot, ["switch", "--track", startPoint], cancellationToken);
    }

    public Task FetchAsync(string worktreePath, string? remote, CancellationToken cancellationToken = default)
        => RunRemoteAsync(
            worktreePath,
            string.IsNullOrWhiteSpace(remote)
                ? ["fetch", "--prune"]
                : ["fetch", "--prune", "--", NormalizeRemoteName(remote)],
            cancellationToken);

    public Task PullAsync(string worktreePath, bool rebase, CancellationToken cancellationToken = default)
        => RunRemoteAsync(worktreePath, rebase ? ["pull", "--rebase"] : ["pull", "--ff-only"], cancellationToken);

    public async Task PushAsync(
        string worktreePath,
        bool forceWithLease,
        bool setUpstream,
        CancellationToken cancellationToken = default)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var arguments = new List<string> { "push" };
        if (forceWithLease)
            arguments.Add("--force-with-lease");
        if (setUpstream)
        {
            var head = await GetHeadStateAsync(repoRoot, cancellationToken);
            if (head.Branch.Length == 0 || string.Equals(head.Branch, "HEAD", StringComparison.Ordinal))
                throw new InvalidOperationException("Push requires a checked-out branch.");

            var remote = RemoteNameFromUpstream(head.Upstream) ?? "origin";
            arguments.Add("-u");
            arguments.Add(remote);
            arguments.Add(head.Branch);
        }

        await RunRemoteAsync(repoRoot, arguments, cancellationToken);
    }

    public async Task<GitLog> GetLogAsync(
        string worktreePath,
        int? max,
        CancellationToken cancellationToken = default)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        var limit = Math.Clamp(max ?? 100, 1, 200);
        var output = await RunGitAsync(
            repoRoot,
            ["log", "--decorate=full", "--pretty=format:%H%x1f%h%x1f%P%x1f%s%x1f%an%x1f%aI%x1f%D", "-n", limit.ToString()],
            cancellationToken,
            allowedExitCodes: [0, 128],
            trimOutput: false);
        return new GitLog(ParseLog(output));
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

    private static async Task<string> NormalizeBranchNameAsync(
        string repoRoot,
        string? name,
        CancellationToken cancellationToken)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Branch is required.");

        if (trimmed.Length > 255
            || trimmed.Contains('\0')
            || trimmed.Contains('\r')
            || trimmed.Contains('\n'))
        {
            throw new InvalidOperationException("Branch must be a valid Git branch name.");
        }

        var result = await RunGitCoreAsync(repoRoot, ["check-ref-format", "--branch", trimmed], cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Branch must be a valid Git branch name.");

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

    private async Task<(string? Upstream, int Ahead, int Behind)> ReadUpstreamAsync(
        string repoRoot,
        CancellationToken cancellationToken)
    {
        var upstreamResult = await RunGitCoreAsync(repoRoot, ["rev-parse", "--abbrev-ref", "@{upstream}"], cancellationToken);
        if (upstreamResult.ExitCode != 0)
            return (null, 0, 0);

        var upstream = upstreamResult.Stdout.TrimEnd();
        var countResult = await RunGitCoreAsync(
            repoRoot,
            ["rev-list", "--left-right", "--count", "HEAD...@{upstream}"],
            cancellationToken);
        if (countResult.ExitCode != 0)
            return (upstream, 0, 0);

        var parts = countResult.Stdout.Trim().Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var ahead)
            || !int.TryParse(parts[1], out var behind))
        {
            return (upstream, 0, 0);
        }

        return (upstream, ahead, behind);
    }

    private static List<GitRemoteBranch> ParseRemoteBranches(string listed)
        => ParseLines(listed)
            .Where(line => !line.EndsWith("/HEAD", StringComparison.Ordinal)
                           && !string.Equals(line, "HEAD", StringComparison.Ordinal))
            .Select(line => (Line: line, Separator: line.IndexOf('/')))
            .Where(item => item.Separator > 0 && item.Separator < item.Line.Length - 1)
            .Select(item => new GitRemoteBranch(item.Line[..item.Separator], item.Line[(item.Separator + 1)..]))
            .DistinctBy(branch => $"{branch.Remote}/{branch.Name}", StringComparer.Ordinal)
            .OrderBy(branch => branch.Remote, StringComparer.OrdinalIgnoreCase)
            .ThenBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<GitRemoteBranch> MatchRemoteBranches(string requested, IReadOnlyList<GitRemoteBranch> remotes)
    {
        var exact = remotes.Where(branch => string.Equals(branch.Name, requested, StringComparison.Ordinal)).ToList();
        if (exact.Count > 0)
            return exact;

        var separator = requested.IndexOf('/');
        if (separator <= 0 || separator == requested.Length - 1)
            return [];

        var remote = requested[..separator];
        var name = requested[(separator + 1)..];
        return remotes
            .Where(branch =>
                string.Equals(branch.Remote, remote, StringComparison.Ordinal)
                && string.Equals(branch.Name, name, StringComparison.Ordinal))
            .ToList();
    }

    private static string? RemoteNameFromUpstream(string? upstream)
    {
        if (string.IsNullOrWhiteSpace(upstream))
            return null;

        var separator = upstream.IndexOf('/');
        return separator <= 0 ? null : upstream[..separator];
    }

    private static string NormalizeRemoteName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > 255 || trimmed.StartsWith('-') || !RemoteNameArgument().IsMatch(trimmed))
            throw new InvalidOperationException("Remote must be a valid Git remote name.");

        return trimmed;
    }

    private static IReadOnlyList<string> ParseLines(string output)
        => output.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<GitLogCommit> ParseLog(string output)
    {
        var commits = new List<GitLogCommit>();
        foreach (var line in ParseLines(output))
        {
            var fields = line.Split('\u001f');
            if (fields.Length < 7)
                continue;

            var parents = fields[2]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var refs = fields[6]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeLogRef)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            commits.Add(new GitLogCommit(
                fields[0],
                fields[1],
                parents,
                fields[3],
                fields[4],
                fields[5],
                refs));
        }

        return commits;
    }

    private static string NormalizeLogRef(string raw)
    {
        var value = raw.Trim();
        if (value.StartsWith("HEAD -> ", StringComparison.Ordinal))
            value = value["HEAD -> ".Length..];
        else if (string.Equals(value, "HEAD", StringComparison.Ordinal))
            return "HEAD";

        if (value.StartsWith("tag: ", StringComparison.Ordinal))
            value = value["tag: ".Length..];
        if (value.StartsWith("refs/heads/", StringComparison.Ordinal))
            return value["refs/heads/".Length..];
        if (value.StartsWith("refs/remotes/", StringComparison.Ordinal))
            return value["refs/remotes/".Length..];
        if (value.StartsWith("refs/tags/", StringComparison.Ordinal))
            return value["refs/tags/".Length..];
        return value;
    }

    private async Task RunRemoteAsync(
        string worktreePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var repoRoot = await RunGitAsync(worktreePath, ["rev-parse", "--show-toplevel"], cancellationToken);
        try
        {
            await RunGitAsync(repoRoot, arguments, cancellationToken, disablePrompt: true);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(MapRemoteError(arguments[0], ex.Message), ex);
        }
    }

    private static string MapRemoteError(string operation, string message)
    {
        if (message.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase)
            || message.Contains("failed to push some refs", StringComparison.OrdinalIgnoreCase))
            return "The remote rejected a non-fast-forward update.";

        if (message.Contains("no upstream", StringComparison.OrdinalIgnoreCase)
            || message.Contains("no tracking information", StringComparison.OrdinalIgnoreCase)
            || message.Contains("has no upstream branch", StringComparison.OrdinalIgnoreCase))
            return "This branch has no upstream. Push with setUpstream to create one.";

        if (message.Contains("Authentication", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("could not read Username", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Could not read from remote repository", StringComparison.OrdinalIgnoreCase)
            || message.Contains("could not read from remote repository", StringComparison.OrdinalIgnoreCase))
            return $"Git {operation} could not authenticate to the remote.";

        return message;
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
        bool trimOutput = true,
        bool disablePrompt = false)
    {
        var allowed = allowedExitCodes ?? [0];
        var result = await RunGitCoreAsync(worktreePath, arguments, cancellationToken, disablePrompt);
        if (!allowed.Contains(result.ExitCode))
            throw new InvalidOperationException($"Git operation '{arguments[0]}' failed: {result.Stderr.Trim()}");

        return trimOutput ? result.Stdout.TrimEnd() : result.Stdout;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunGitCoreAsync(
        string worktreePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool disablePrompt = false)
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
        if (disablePrompt)
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
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

    [GeneratedRegex("^(rev-parse|status|diff|add|commit|cat-file|restore|clean|branch|switch|check-ref-format|fetch|pull|push|log|rev-list)$")]
    private static partial Regex AllowedGitOperation();

    [GeneratedRegex(@"^[^\u0000-\u001F\u007F]+$")]
    private static partial Regex GitPathArgument();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex RemoteNameArgument();
}
