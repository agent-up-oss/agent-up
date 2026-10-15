using System.ComponentModel;
using System.Diagnostics;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Interfaces;

namespace AgentUp.Server.Features.SourceClones.Providers;

public sealed class GitSourceCloneProvider : ISourceCloneGitProvider
{
    private readonly string _gitExecutable;

    public GitSourceCloneProvider()
        : this("git")
    {
    }

    internal GitSourceCloneProvider(string gitExecutable)
    {
        _gitExecutable = gitExecutable;
    }

    public async Task CloneAsync(SourceCloneTarget target, CancellationToken cancellationToken = default)
    {
        var parent = Path.GetDirectoryName(target.DestinationPath)
            ?? throw new InvalidOperationException("Source clone destination must have a parent directory.");
        Directory.CreateDirectory(parent);

        if (target.Derived && LooksLikeCommitSha(target.Branch))
        {
            await RunGitAsync(
                parent,
                ["clone", "--", target.Repository, target.DestinationPath],
                $"Cloning '{target.Repository}' failed",
                cancellationToken);
            await RunGitAsync(
                target.DestinationPath,
                ["checkout", "--detach", target.Branch],
                $"Checking out revision '{target.Branch}' failed",
                cancellationToken);
            return;
        }

        await RunGitAsync(
            parent,
            ["clone", "--branch", target.Branch, "--", target.Repository, target.DestinationPath],
            $"Cloning '{target.Repository}' at branch '{target.Branch}' failed",
            cancellationToken);
    }

    public async Task RefreshAsync(string destinationPath, string revision, CancellationToken cancellationToken = default)
    {
        try
        {
            await RunGitAsync(
                destinationPath,
                ["fetch", "--force", "origin", revision],
                $"Refreshing '{destinationPath}' at '{revision}' failed",
                cancellationToken);
            await RunGitAsync(
                destinationPath,
                ["checkout", "--detach", "FETCH_HEAD"],
                $"Checking out refreshed revision '{revision}' failed",
                cancellationToken);
        }
        catch (InvalidOperationException) when (LooksLikeCommitSha(revision))
        {
            await RunGitAsync(
                destinationPath,
                ["fetch", "origin"],
                $"Refreshing '{destinationPath}' failed",
                cancellationToken);
            await RunGitAsync(
                destinationPath,
                ["checkout", "--detach", revision],
                $"Checking out revision '{revision}' failed",
                cancellationToken);
        }
    }

    internal static bool LooksLikeCommitSha(string revision)
        => revision.Length is >= 7 and <= 40 && revision.All(Uri.IsHexDigit);

    private async Task RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string failurePrefix,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(_gitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"git could not be started: {ex.Message}", ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        string stderr;
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await stdoutTask;
            stderr = await stderrTask;
        }
        catch (OperationCanceledException)
        {
            // Disposing the process does not stop git, so an abandoned clone would keep writing
            // into the source clones root after the request ended.
            await KillProcessAfterCancellationAsync(process);
            throw;
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{failurePrefix}: {stderr.Trim()}");
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
}
