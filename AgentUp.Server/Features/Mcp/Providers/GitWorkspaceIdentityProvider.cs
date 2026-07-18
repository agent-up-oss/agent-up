using System.Diagnostics;
using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Interfaces;

namespace AgentUp.Server.Features.Mcp.Providers;

public sealed class GitWorkspaceIdentityProvider : IWorkspaceIdentityProvider
{
    public async Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken)
    {
        try
        {
            return new WorkspaceIdentity(
                RepositoryPath: await GetRepoRootAsync(worktreePath, cancellationToken),
                Branch: await RunGitAsync(worktreePath, "rev-parse --abbrev-ref HEAD", cancellationToken),
                Commit: await RunGitAsync(worktreePath, "rev-parse HEAD", cancellationToken));
        }
        catch
        {
            return new WorkspaceIdentity(
                RepositoryPath: worktreePath,
                Branch: "not on a git branch",
                Commit: "");
        }
    }

    private static async Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var commonDir = await RunGitAsync(worktreePath, "rev-parse --git-common-dir", cancellationToken);
        if (Path.IsPathRooted(commonDir))
            return Path.GetDirectoryName(commonDir)!;

        return await RunGitAsync(worktreePath, "rev-parse --show-toplevel", cancellationToken);
    }

    private static async Task<string> RunGitAsync(
        string worktreePath,
        string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = worktreePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {arguments} failed: {stderr.Trim()}");

        return stdout.Trim();
    }
}
