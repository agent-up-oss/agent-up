using System.Diagnostics;
using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;

namespace AgentUp.CLI.Features.Capabilities.Providers;

public sealed class CapabilityPackProcessRunner : ICapabilityPackProcessRunner
{
    private static readonly HashSet<string> AllowedNames = new(StringComparer.Ordinal) { "bash" };

    public async Task<CapabilityProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedNames.Contains(fileName))
            throw new InvalidOperationException($"Command '{fileName}' is not allowlisted.");

        var start = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new CapabilityProcessResult(process.ExitCode, await stdout, await stderr);
    }
}
