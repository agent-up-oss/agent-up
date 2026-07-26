using System.Diagnostics;
using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Providers;

public sealed class CommitsGitProvider(string workingDirectory) : ICommitsGitProvider
{
    public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
        => RunGitAsync(["rev-parse", "--show-toplevel"], cancellationToken);

    public async Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await RunGitAsync(["status", "--porcelain"], cancellationToken);
            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Length > 3 ? line[3..].Trim() : "")
                .Where(path => path.Length > 0)
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    public async Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "add", "--" };
        args.AddRange(files);
        await RunGitAsync(args, cancellationToken);
    }

    private async Task<string> RunGitAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(" ", arguments)} failed: {stderr.Trim()}");

        return stdout.TrimEnd();
    }
}
