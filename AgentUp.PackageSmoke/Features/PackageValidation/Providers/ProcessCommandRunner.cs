using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using System.Diagnostics;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Providers;

public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
    {
        if (!TryNormalize(command, out var safeCommand, out var validationError))
            return new CommandResult(126, "", validationError);

        var startInfo = new ProcessStartInfo
        {
            FileName = safeCommand.FileName,
            WorkingDirectory = safeCommand.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in safeCommand.Arguments)
            startInfo.ArgumentList.Add(argument);

        if (safeCommand.Environment is not null)
        {
            foreach (var (key, value) in safeCommand.Environment)
                startInfo.Environment[key] = value;
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new CommandResult(127, "", ex.Message);
        }

        if (process is null)
            return new CommandResult(127, "", $"Failed to start {safeCommand.FileName}.");

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new CommandResult(process.ExitCode, await stdout, await stderr);
        }
    }

    private static bool TryNormalize(CommandSpec command, out CommandSpec safeCommand, out string error)
    {
        safeCommand = command;

        if (!TryNormalizeFileName(command.FileName, out var fileName, out error))
            return false;

        if (!TryNormalizeWorkingDirectory(command.WorkingDirectory, out var workingDirectory, out error))
            return false;

        var arguments = new List<string>(command.Arguments.Count);
        foreach (var argument in command.Arguments)
        {
            if (argument.IndexOfAny(['\0', '\r', '\n']) >= 0)
            {
                error = "Command arguments must not contain control characters.";
                return false;
            }

            arguments.Add(argument);
        }

        Dictionary<string, string>? environment = null;
        if (command.Environment is not null)
        {
            environment = [];
            foreach (var (key, value) in command.Environment)
            {
                if (!IsSafeEnvironmentKey(key))
                {
                    error = $"Environment variable name '{key}' is not allowed.";
                    return false;
                }

                if (value.IndexOfAny(['\0', '\r', '\n']) >= 0)
                {
                    error = $"Environment variable '{key}' must not contain control characters.";
                    return false;
                }

                environment.Add(key, value);
            }
        }

        safeCommand = new CommandSpec(fileName, arguments, workingDirectory, environment);
        error = "";
        return true;
    }

    private static bool TryNormalizeFileName(string fileName, out string safeFileName, out string error)
    {
        safeFileName = "";

        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(['\0', '\r', '\n', '"']) >= 0)
        {
            error = "Command executable name is not allowed.";
            return false;
        }

        if (TryGetKnownInstalledExecutable(fileName, out safeFileName))
        {
            error = "";
            return true;
        }

        if (Path.GetFileName(fileName) != fileName)
        {
            error = "Command executable paths must be known Agent-Up installed executables.";
            return false;
        }

        return TryGetAllowedCommandName(fileName, out safeFileName, out error);
    }

    private static bool TryNormalizeWorkingDirectory(string? workingDirectory, out string safeWorkingDirectory, out string error)
    {
        if (workingDirectory is null)
        {
            safeWorkingDirectory = Environment.CurrentDirectory;
            error = "";
            return true;
        }

        if (workingDirectory.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
            !Path.IsPathFullyQualified(workingDirectory) ||
            !Directory.Exists(workingDirectory))
        {
            safeWorkingDirectory = "";
            error = "Command working directory must be an absolute existing directory.";
            return false;
        }

        safeWorkingDirectory = Path.GetFullPath(workingDirectory);
        error = "";
        return true;
    }

    private static bool IsSafeEnvironmentKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key[0] != '_' && !char.IsAsciiLetter(key[0]))
            return false;

        foreach (var character in key)
        {
            if (character != '_' && !char.IsAsciiLetterOrDigit(character))
                return false;
        }

        return true;
    }

    private static bool TryGetAllowedCommandName(string fileName, out string safeFileName, out string error)
    {
        safeFileName = fileName switch
        {
            "agent-up" => "agent-up",
            "agent-up.cmd" => "agent-up.cmd",
            "bash" => "bash",
            "dpkg-deb" => "dpkg-deb",
            "git" => "git",
            "lsof" => "lsof",
            "msiexec.exe" => "msiexec.exe",
            "pkgutil" => "pkgutil",
            "powershell.exe" => "powershell.exe",
            "ps" => "ps",
            "sc.exe" => "sc.exe",
            "ss" => "ss",
            "sudo" => "sudo",
            _ => ""
        };

        if (safeFileName.Length > 0)
        {
            error = "";
            return true;
        }

        error = "Command executable name is not allowed.";
        return false;
    }

    private static bool TryGetKnownInstalledExecutable(string fileName, out string safeFileName)
    {
        if (!Path.IsPathFullyQualified(fileName) || !File.Exists(fileName))
        {
            safeFileName = "";
            return false;
        }

        var fullPath = Path.GetFullPath(fileName);
        var knownPath = fullPath switch
        {
            "/usr/bin/agent-up" => "/usr/bin/agent-up",
            "/usr/local/bin/agent-up" => "/usr/local/bin/agent-up",
            _ => ""
        };

        if (knownPath.Length > 0)
        {
            safeFileName = knownPath;
            return true;
        }

        var windowsCli = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Agent-Up",
            "cli",
            "AgentUp.CLI.exe"));

        if (OperatingSystem.IsWindows() && string.Equals(fullPath, windowsCli, StringComparison.OrdinalIgnoreCase))
        {
            safeFileName = windowsCli;
            return true;
        }

        safeFileName = "";
        return false;
    }
}
