using System.Diagnostics;
using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Interfaces;

namespace AgentUp.Server.Features.Mcp.Providers;

public sealed class GitWorkspaceIdentityProvider : IWorkspaceIdentityProvider
{
    private const string GitExecutable = "git";

    public async Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken)
    {
        try
        {
            return new WorkspaceIdentity(
                RepositoryPath: await GetRepoRootAsync(worktreePath, cancellationToken),
                Branch: await RunGitAsync(worktreePath, GitCommand.BranchName, cancellationToken),
                Commit: await RunGitAsync(worktreePath, GitCommand.HeadCommit, cancellationToken));
        }
        catch (InvalidOperationException)
        {
            return CreateFallbackIdentity(worktreePath);
        }
        catch (IOException)
        {
            return CreateFallbackIdentity(worktreePath);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateFallbackIdentity(worktreePath);
        }
    }

    private static WorkspaceIdentity CreateFallbackIdentity(string worktreePath) =>
        new(
            RepositoryPath: worktreePath,
            Branch: "not on a git branch",
            Commit: "");

    private static async Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var commonDir = await RunGitAsync(worktreePath, GitCommand.GitCommonDir, cancellationToken);
        if (Path.IsPathRooted(commonDir))
            return Path.GetDirectoryName(commonDir)!;

        return await RunGitAsync(worktreePath, GitCommand.ShowTopLevel, cancellationToken);
    }

    private static async Task<string> RunGitAsync(
        string worktreePath,
        GitCommand command,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GitExecutable,
            WorkingDirectory = worktreePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in GetGitArguments(command))
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {GetGitCommandName(command)} failed: {stderr.Trim()}");

        return stdout.Trim();
    }

    private static IReadOnlyList<string> GetGitArguments(GitCommand command) =>
        command switch
        {
            GitCommand.BranchName => ["rev-parse", "--abbrev-ref", "HEAD"],
            GitCommand.HeadCommit => ["rev-parse", "HEAD"],
            GitCommand.GitCommonDir => ["rev-parse", "--git-common-dir"],
            GitCommand.ShowTopLevel => ["rev-parse", "--show-toplevel"],
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported git command.")
        };

    private static string GetGitCommandName(GitCommand command) =>
        string.Join(' ', GetGitArguments(command));

    private enum GitCommand
    {
        BranchName,
        HeadCommit,
        GitCommonDir,
        ShowTopLevel
    }
}
