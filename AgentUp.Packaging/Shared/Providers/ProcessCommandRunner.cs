using AgentUp.Packaging.Shared.Interfaces;
using System.Diagnostics;

namespace AgentUp.Packaging.Shared.Providers;

public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            WorkingDirectory = command.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in command.Arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {command.FileName}.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var result = new CommandResult(process.ExitCode, await stdout, await stderr);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{command.FileName} exited with code {result.ExitCode}.{Environment.NewLine}{result.Stderr}{result.Stdout}");

        return result;
    }
}
