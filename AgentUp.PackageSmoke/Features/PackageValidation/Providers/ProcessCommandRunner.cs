using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using System.Diagnostics;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Providers;

public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
    {
        if (!TryNormalize(command, out var safeCommand, out var validationError))
            return new CommandResult(126, "", validationError);

        var startInfo = CreateStartInfo(safeCommand.Executable);
        startInfo.WorkingDirectory = safeCommand.WorkingDirectory;

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
            return new CommandResult(127, "", $"Failed to start {safeCommand.DisplayName}.");

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new CommandResult(process.ExitCode, await stdout, await stderr);
        }
    }

    private static ProcessStartInfo CreateStartInfo(SmokeExecutable executable)
    {
        var startInfo = executable switch
        {
            SmokeExecutable.AgentUp => new ProcessStartInfo("agent-up"),
            SmokeExecutable.AgentUpCmd => new ProcessStartInfo("agent-up.cmd"),
            SmokeExecutable.Bash => new ProcessStartInfo("bash"),
            SmokeExecutable.DpkgDeb => new ProcessStartInfo("dpkg-deb"),
            SmokeExecutable.Git => new ProcessStartInfo("git"),
            SmokeExecutable.Lsof => new ProcessStartInfo("lsof"),
            SmokeExecutable.Msiexec => new ProcessStartInfo("msiexec.exe"),
            SmokeExecutable.Pkgutil => new ProcessStartInfo("pkgutil"),
            SmokeExecutable.PowerShell => new ProcessStartInfo("powershell.exe"),
            SmokeExecutable.Ps => new ProcessStartInfo("ps"),
            SmokeExecutable.Sc => new ProcessStartInfo("sc.exe"),
            SmokeExecutable.Ss => new ProcessStartInfo("ss"),
            SmokeExecutable.Sudo => new ProcessStartInfo("sudo"),
            _ => throw new ArgumentOutOfRangeException(nameof(executable), executable, "Unsupported smoke executable.")
        };

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        return startInfo;
    }

    private static bool TryNormalize(CommandSpec command, out SafeCommandSpec safeCommand, out string error)
    {
        safeCommand = default;

        if (!TryNormalizeFileName(command.FileName, out var executable, out error))
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

        safeCommand = new SafeCommandSpec(executable, DisplayName(executable), arguments, workingDirectory, environment);
        error = "";
        return true;
    }

    private static bool TryNormalizeFileName(string fileName, out SmokeExecutable executable, out string error)
    {
        executable = default;

        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(['\0', '\r', '\n', '"']) >= 0)
        {
            error = "Command executable name is not allowed.";
            return false;
        }

        if (Path.GetFileName(fileName) != fileName)
        {
            error = "Command executable paths are not allowed.";
            return false;
        }

        return TryGetAllowedCommandName(fileName, out executable, out error);
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

    private static bool TryGetAllowedCommandName(string fileName, out SmokeExecutable executable, out string error)
    {
        executable = fileName switch
        {
            "agent-up" => SmokeExecutable.AgentUp,
            "agent-up.cmd" => SmokeExecutable.AgentUpCmd,
            "bash" => SmokeExecutable.Bash,
            "dpkg-deb" => SmokeExecutable.DpkgDeb,
            "git" => SmokeExecutable.Git,
            "lsof" => SmokeExecutable.Lsof,
            "msiexec.exe" => SmokeExecutable.Msiexec,
            "pkgutil" => SmokeExecutable.Pkgutil,
            "powershell.exe" => SmokeExecutable.PowerShell,
            "ps" => SmokeExecutable.Ps,
            "sc.exe" => SmokeExecutable.Sc,
            "ss" => SmokeExecutable.Ss,
            "sudo" => SmokeExecutable.Sudo,
            _ => SmokeExecutable.Unknown
        };

        if (executable != SmokeExecutable.Unknown)
        {
            error = "";
            return true;
        }

        error = "Command executable name is not allowed.";
        return false;
    }

    private static string DisplayName(SmokeExecutable executable)
        => executable switch
        {
            SmokeExecutable.AgentUp => "agent-up",
            SmokeExecutable.AgentUpCmd => "agent-up.cmd",
            SmokeExecutable.Bash => "bash",
            SmokeExecutable.DpkgDeb => "dpkg-deb",
            SmokeExecutable.Git => "git",
            SmokeExecutable.Lsof => "lsof",
            SmokeExecutable.Msiexec => "msiexec.exe",
            SmokeExecutable.Pkgutil => "pkgutil",
            SmokeExecutable.PowerShell => "powershell.exe",
            SmokeExecutable.Ps => "ps",
            SmokeExecutable.Sc => "sc.exe",
            SmokeExecutable.Ss => "ss",
            SmokeExecutable.Sudo => "sudo",
            _ => throw new ArgumentOutOfRangeException(nameof(executable), executable, "Unsupported smoke executable.")
        };

    private readonly record struct SafeCommandSpec(
        SmokeExecutable Executable,
        string DisplayName,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory,
        IReadOnlyDictionary<string, string>? Environment);

    private enum SmokeExecutable
    {
        Unknown,
        AgentUp,
        AgentUpCmd,
        Bash,
        DpkgDeb,
        Git,
        Lsof,
        Msiexec,
        Pkgutil,
        PowerShell,
        Ps,
        Sc,
        Ss,
        Sudo
    }
}
