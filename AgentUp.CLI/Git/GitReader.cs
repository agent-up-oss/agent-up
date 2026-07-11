using System.Diagnostics;

namespace AgentUp.CLI.Git;

public sealed class GitReader
{
    private readonly string _workingDirectory;

    public GitReader(string workingDirectory) => _workingDirectory = workingDirectory;

    public Task<string> GetBranchAsync() =>
        RunGitAsync("rev-parse --abbrev-ref HEAD");

    public Task<string> GetCommitAsync() =>
        RunGitAsync("rev-parse HEAD");

    public async Task<string> GetRepoRootAsync()
    {
        var commonDir = await RunGitAsync("rev-parse --git-common-dir");
        if (Path.IsPathRooted(commonDir))
            return Path.GetDirectoryName(commonDir)!;
        return await RunGitAsync("rev-parse --show-toplevel");
    }

    private async Task<string> RunGitAsync(string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {arguments} failed: {stderr.Trim()}");

        return stdout.Trim();
    }
}
