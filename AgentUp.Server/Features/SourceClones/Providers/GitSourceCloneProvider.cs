using System.Diagnostics;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Interfaces;

namespace AgentUp.Server.Features.SourceClones.Providers;

public sealed class GitSourceCloneProvider : ISourceCloneGitProvider
{
    public async Task CloneAsync(SourceCloneTarget target, CancellationToken cancellationToken = default)
    {
        var parent = Path.GetDirectoryName(target.DestinationPath)
            ?? throw new InvalidOperationException("Source clone destination must have a parent directory.");
        Directory.CreateDirectory(parent);

        var psi = new ProcessStartInfo("git")
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

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Cloning '{target.Repository}' at branch '{target.Branch}' failed: {stderr.Trim()}");
    }
}
