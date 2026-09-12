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

        var psi = new ProcessStartInfo(_gitExecutable)
        {
            WorkingDirectory = parent,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.ArgumentList.Add("clone");
        psi.ArgumentList.Add("--branch");
        psi.ArgumentList.Add(target.Branch);
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(target.Repository);
        psi.ArgumentList.Add(target.DestinationPath);

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
            throw new InvalidOperationException($"Cloning '{target.Repository}' at branch '{target.Branch}' failed: {stderr.Trim()}");
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
