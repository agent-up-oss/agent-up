using System.Diagnostics;

namespace AgentUp.Server.Tests.Support;

internal static class TestGitRepository
{
    public static async Task<string> InitializeAsync(string path, string initialBranch = "main")
    {
        Directory.CreateDirectory(path);
        await RunAsync(path, "init", "--initial-branch", initialBranch);
        await RunAsync(path, "config", "user.name", "Agent Up");
        await RunAsync(path, "config", "user.email", "agent-up@example.invalid");
        await RunAsync(path, "config", "commit.gpgsign", "false");
        return path;
    }

    public static async Task CommitAllAsync(string path, string message)
    {
        await RunAsync(path, "add", "--all");
        await RunAsync(path, "commit", "-m", message);
    }

    public static Task RunAsync(string workingDirectory, params string[] arguments)
        => RunAsync(workingDirectory, arguments, [0]);

    public static async Task<string> ReadAsync(string workingDirectory, params string[] arguments)
    {
        var (stdout, _, _) = await ExecuteAsync(workingDirectory, arguments);
        return stdout.TrimEnd();
    }

    public static async Task RunAsync(string workingDirectory, string[] arguments, int[] allowedExitCodes)
    {
        var (_, stderr, exitCode) = await ExecuteAsync(workingDirectory, arguments);
        if (!allowedExitCodes.Contains(exitCode))
            throw new InvalidOperationException($"git {string.Join(" ", arguments)} failed: {stderr.Trim()}");
    }

    private static async Task<(string Stdout, string Stderr, int ExitCode)> ExecuteAsync(
        string workingDirectory,
        string[] arguments)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (await stdoutTask, await stderrTask, process.ExitCode);
    }
}
