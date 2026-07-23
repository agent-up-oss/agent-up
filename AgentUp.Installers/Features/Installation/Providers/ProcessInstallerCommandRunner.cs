using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Features.Installation.Providers;

public sealed class ProcessInstallerCommandRunner : ICommandRunner
{
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.Ordinal)
    {
        "bash",
        "docker",
        "dpkg-query",
        "launchctl",
        "osascript",
        "powershell.exe",
        "sc.exe",
        "systemctl",
        "update-desktop-database"
    };

    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        if (!AllowedCommands.Contains(fileName))
            return new ProcessResult(127, "", $"Unsupported installer command: {fileName}.");

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        System.Diagnostics.Process? process;
        try
        {
            process = System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new ProcessResult(127, "", ex.Message);
        }

        if (process is null)
            return new ProcessResult(127, "", $"Failed to start {fileName}.");

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult(process.ExitCode, await stdout, await stderr);
        }
    }
}
