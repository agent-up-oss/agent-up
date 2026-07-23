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

    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
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
        foreach (var argument in SplitArguments(arguments))
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

    private static IReadOnlyList<string> SplitArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return [];

        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quote = '\0';
        for (var index = 0; index < arguments.Length; index++)
        {
            var character = arguments[index];
            if (quote == '\0' && char.IsWhiteSpace(character))
            {
                AddCurrent();
                continue;
            }

            if (character is '"' or '\'' && (quote == '\0' || quote == character))
            {
                quote = quote == '\0' ? character : '\0';
                continue;
            }

            current.Append(character);
        }

        AddCurrent();
        return result;

        void AddCurrent()
        {
            if (current.Length == 0)
                return;

            result.Add(current.ToString());
            current.Clear();
        }
    }
}
