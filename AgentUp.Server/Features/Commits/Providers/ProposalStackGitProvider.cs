using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Features.Commits.Providers;

public sealed class ProposalStackGitProvider(ICommitsGitProvider diffs, string? storageRoot = null) : IProposalStackGitProvider
{
    public async Task<ProposalCommitResult> EnqueueAsync(
        string worktreePath,
        CommitsQueue current,
        string queueId,
        string message,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken = default)
    {
        var repositoryRoot = await diffs.GetRepoRootAsync(worktreePath, cancellationToken);
        var queueRef = $"refs/agent-up/queues/{queueId}/tip";
        var patch = await diffs.GetDiffAsync(worktreePath, files, cancellationToken);
        if (string.IsNullOrWhiteSpace(patch))
            throw new InvalidOperationException("The selected files contain no changes to enqueue.");

        var firstEntry = current.Commits.Count == 0;
        var baseCommit = firstEntry
            ? await GitAsync(repositoryRoot, ["rev-parse", "HEAD"], cancellationToken)
            : current.BaseCommit ?? throw new InvalidOperationException("The proposal queue has no base commit.");
        var parentCommit = firstEntry
            ? baseCommit
            : current.TipCommit ?? throw new InvalidOperationException("The proposal queue has no tip commit.");
        var queueWorktree = firstEntry
            ? await QueueWorktreeAsync(repositoryRoot, queueId, cancellationToken)
            : current.QueueWorktreePath ?? throw new InvalidOperationException("The proposal queue has no managed worktree.");

        if (firstEntry)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(queueWorktree)!);
            if (Directory.Exists(queueWorktree))
                throw new InvalidOperationException("The proposal queue worktree already exists. Recover or remove it before enqueueing.");
            await GitAsync(repositoryRoot, ["worktree", "add", "--detach", queueWorktree, baseCommit], cancellationToken);
        }
        else
        {
            var suppliedRoot = Path.GetFullPath(repositoryRoot);
            var expectedRoot = Path.GetFullPath(queueWorktree);
            if (!string.Equals(suppliedRoot, expectedRoot, PathComparison))
                throw new InvalidOperationException($"Continue dependent work in the managed queue worktree: {queueWorktree}");
            var actualHead = await GitAsync(queueWorktree, ["rev-parse", "HEAD"], cancellationToken);
            if (!string.Equals(actualHead, parentCommit, StringComparison.Ordinal))
                throw new InvalidOperationException("The managed queue worktree no longer matches the recorded queue tip.");
        }

        try
        {
            if (firstEntry)
                await ApplyPatchAsync(queueWorktree, patch, cancellationToken);
            else
                await GitAsync(queueWorktree, ["add", "--", .. files], cancellationToken);

            await GitAsync(
                queueWorktree,
                ["-c", "user.name=Agent-Up Proposal Queue", "-c", "user.email=agent-up@localhost", "commit", "--no-verify", "-m", message, "--", .. files],
                cancellationToken);
            var commit = await GitAsync(queueWorktree, ["rev-parse", "HEAD"], cancellationToken);
            await GitAsync(repositoryRoot, ["update-ref", queueRef, commit, firstEntry ? ZeroObjectId : parentCommit], cancellationToken);

            if (firstEntry)
                await diffs.RestoreFilesAsync(worktreePath, files, cancellationToken);

            return new ProposalCommitResult(baseCommit, parentCommit, commit, queueWorktree, queueRef, patch);
        }
        catch (InvalidOperationException)
        {
            if (firstEntry)
                _ = await RemoveFailedWorktreeAsync(repositoryRoot, queueWorktree);
            throw;
        }
        catch (IOException)
        {
            if (firstEntry)
                _ = await RemoveFailedWorktreeAsync(repositoryRoot, queueWorktree);
            throw;
        }
    }

    private async Task<string> QueueWorktreeAsync(string repositoryRoot, string queueId, CancellationToken cancellationToken)
    {
        var root = storageRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var commonDirectory = await GitAsync(repositoryRoot, ["rev-parse", "--git-common-dir"], cancellationToken);
        var commonPath = Path.IsPathRooted(commonDirectory) ? commonDirectory : Path.Join(repositoryRoot, commonDirectory);
        var identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(commonPath))))[..16];
        return Path.Join(root, "agentup", "commits", "worktrees", identity, queueId);
    }

    private static async Task ApplyPatchAsync(string worktree, string patch, CancellationToken cancellationToken)
    {
        var start = StartInfo(worktree, ["apply", "--index", "--binary", "--whitespace=nowarn"]);
        start.RedirectStandardInput = true;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start Git patch application.");
        await process.StandardInput.WriteAsync(patch.AsMemory(), cancellationToken);
        process.StandardInput.Close();
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Could not apply the proposal patch: {error.Trim()}");
    }

    private static async Task<bool> RemoveFailedWorktreeAsync(string repositoryRoot, string queueWorktree)
    {
        try
        {
            await GitAsync(repositoryRoot, ["worktree", "remove", "--force", queueWorktree], CancellationToken.None);
            return true;
        }
        catch (InvalidOperationException)
        {
            // The original failure remains authoritative; Git may already have removed it.
            return false;
        }
    }

    private static async Task<string> GitAsync(string worktree, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = Process.Start(StartInfo(worktree, arguments))
            ?? throw new InvalidOperationException("Failed to start Git.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Proposal queue Git operation failed: {error.Trim()}");
        return output.Trim();
    }

    private static ProcessStartInfo StartInfo(string worktree, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(Path.GetFullPath(worktree));
        foreach (var argument in arguments)
        {
            if (argument.Contains('\0') || argument.Contains('\r') || argument.Contains('\n'))
                throw new InvalidOperationException("Proposal queue Git arguments must be literal values.");
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private const string ZeroObjectId = "0000000000000000000000000000000000000000";
}
