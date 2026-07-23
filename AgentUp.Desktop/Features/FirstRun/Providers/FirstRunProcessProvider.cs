using System.Diagnostics;
using AgentUp.Desktop.Features.FirstRun.DTOs;
using AgentUp.Desktop.Features.FirstRun.Interfaces;

namespace AgentUp.Desktop.Features.FirstRun.Providers;

public sealed class FirstRunProcessProvider : IFirstRunProcessProvider
{
    public async Task<FirstRunProcessResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        using var process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException($"{fileName} could not be started.");

        var exited = await WaitForExitAsync(process, timeout, cancellationToken);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Trace.TraceWarning(ex.Message);
            }

            return new FirstRunProcessResult(-1, "", $"{fileName} did not respond in time.");
        }

        var output = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
        var error = (await process.StandardError.ReadToEndAsync(cancellationToken)).Trim();
        return new FirstRunProcessResult(process.ExitCode, output, error);
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var delayTask = Task.Delay(timeout, cancellationToken);
        return await Task.WhenAny(exitTask, delayTask) == exitTask;
    }
}
