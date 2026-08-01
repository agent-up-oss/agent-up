using System.Diagnostics;

namespace AgentUp.Server.Features.Audit.Providers;

public sealed class AuditGitStateProvider
{
    public async Task<(string? Branch, string? Commit, bool? Dirty)> ReadAsync(
        string worktreePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var branch = await RunGitAsync(worktreePath, cancellationToken, "rev-parse", "--abbrev-ref", "HEAD");
            var commit = await RunGitAsync(worktreePath, cancellationToken, "rev-parse", "HEAD");
            var status = await RunGitAsync(worktreePath, cancellationToken, "status", "--porcelain");
            return (
                string.IsNullOrWhiteSpace(branch) ? null : branch.Trim(),
                string.IsNullOrWhiteSpace(commit) ? null : commit.Trim(),
                !string.IsNullOrWhiteSpace(status));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            return (null, null, null);
        }
    }

    private static async Task<string> RunGitAsync(
        string worktreePath,
        CancellationToken cancellationToken,
        params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path.GetFullPath(worktreePath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("git did not start.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        await stderrTask;
        return process.ExitCode == 0 ? stdout.Trim() : string.Empty;
    }
}
