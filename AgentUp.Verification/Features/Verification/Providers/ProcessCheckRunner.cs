using System.Diagnostics;
using System.Text;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Executes a check's command through the platform shell and captures its output.
/// </summary>
public sealed class ProcessCheckRunner : ICheckRunner
{
    public async Task<CheckOutcome> RunAsync(
        string repositoryRoot,
        CheckDefinition check,
        CancellationToken cancellationToken)
    {
        var workingDirectory = check.WorkingDirectory is null
            ? repositoryRoot
            : Path.Join(repositoryRoot, check.WorkingDirectory);

        var startInfo = CreateStartInfo(check.Command, workingDirectory);
        var stopwatch = Stopwatch.StartNew();

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new CheckOutcome(
                check.Id, check.Command, -1, stopwatch.ElapsedMilliseconds,
                $"Could not start a shell for check '{check.Id}'.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        stopwatch.Stop();

        var output = new StringBuilder()
            .Append(await standardOutput)
            .Append(await standardError)
            .ToString();

        return new CheckOutcome(
            check.Id,
            check.Command,
            process.ExitCode,
            stopwatch.ElapsedMilliseconds,
            Truncate(output));
    }

    private static ProcessStartInfo CreateStartInfo(string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);
            return startInfo;
        }

        startInfo.FileName = "/bin/bash";
        startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add(command);
        return startInfo;
    }

    /// <summary>
    /// Keeps the tail of long output: a failing check's useful lines are at the end.
    /// </summary>
    private static string Truncate(string output)
    {
        const int limit = 20000;
        return output.Length <= limit ? output : output[^limit..];
    }
}
