using System.Diagnostics;
using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Providers;

public sealed class CommitsGitProvider(string workingDirectory) : ICommitsGitProvider
{
    public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
        => RunGitAsync("rev-parse --show-toplevel", cancellationToken);

    public async Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunGitAsync("status --porcelain", cancellationToken);
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Length > 3 ? line[3..].Trim() : "")
            .Where(path => path.Length > 0)
            .ToList();
    }

    public async Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        var args = string.Join(" ", files.Select(f => $"\"{f}\""));
        await RunGitAsync($"add -- {args}", cancellationToken);
    }

    private async Task<string> RunGitAsync(string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
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
