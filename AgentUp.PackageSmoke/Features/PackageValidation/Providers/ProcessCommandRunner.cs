using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Models;
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

        if (!TryAddAllowedArguments(startInfo, safeCommand, out validationError))
            return new CommandResult(126, "", validationError);

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
            SmokeExecutable.Cmd => new ProcessStartInfo("cmd.exe"),
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

    private static bool TryAddAllowedArguments(ProcessStartInfo startInfo, SafeCommandSpec command, out string error)
    {
        error = "";

        if (command.Executable == SmokeExecutable.AgentUp && IsArguments(command, "--version"))
        {
            startInfo.ArgumentList.Add("--version");
            return true;
        }

        if (command.Executable == SmokeExecutable.AgentUp && IsArguments(command, "start"))
        {
            startInfo.ArgumentList.Add("start");
            return true;
        }

        if (command.Executable == SmokeExecutable.AgentUp && IsArguments(command, "status"))
        {
            startInfo.ArgumentList.Add("status");
            return true;
        }

        if (command.Executable == SmokeExecutable.AgentUpCmd && IsArguments(command, "--version"))
        {
            startInfo.ArgumentList.Add("--version");
            return true;
        }

        if (command.Executable == SmokeExecutable.AgentUpCmd && IsArguments(command, "start"))
        {
            startInfo.ArgumentList.Add("start");
            return true;
        }

        if (command.Executable == SmokeExecutable.AgentUpCmd && IsArguments(command, "status"))
        {
            startInfo.ArgumentList.Add("status");
            return true;
        }

        if (command.Executable == SmokeExecutable.Cmd && IsArguments(command, "/C", "agent-up.cmd", "--version"))
        {
            startInfo.ArgumentList.Add("/C");
            startInfo.ArgumentList.Add("agent-up.cmd");
            startInfo.ArgumentList.Add("--version");
            return true;
        }

        if (command.Executable == SmokeExecutable.Cmd && IsArguments(command, "/C", "agent-up.cmd", "start"))
        {
            startInfo.ArgumentList.Add("/C");
            startInfo.ArgumentList.Add("agent-up.cmd");
            startInfo.ArgumentList.Add("start");
            return true;
        }

        if (command.Executable == SmokeExecutable.Cmd && IsArguments(command, "/C", "agent-up.cmd", "status"))
        {
            startInfo.ArgumentList.Add("/C");
            startInfo.ArgumentList.Add("agent-up.cmd");
            startInfo.ArgumentList.Add("status");
            return true;
        }

        if (command.Executable == SmokeExecutable.Git)
            return TryAddGitArguments(startInfo, command, out error);

        if (command.Executable == SmokeExecutable.DpkgDeb)
            return TryAddDpkgDebArguments(startInfo, command, out error);

        if (command.Executable == SmokeExecutable.Pkgutil)
            return TryAddPkgutilArguments(startInfo, command, out error);

        if (command.Executable == SmokeExecutable.Msiexec)
            return TryAddMsiexecArguments(startInfo, command, out error);

        if (command.Executable == SmokeExecutable.PowerShell)
            return TryAddPowerShellArguments(startInfo, command, out error);

        if (command.Executable == SmokeExecutable.Sc && IsArguments(command, "start", "agent-up-server"))
        {
            startInfo.ArgumentList.Add("start");
            startInfo.ArgumentList.Add("agent-up-server");
            return true;
        }

        if (command.Executable == SmokeExecutable.Bash && IsArguments(command, "-lc", "command -v agent-up"))
        {
            startInfo.ArgumentList.Add("-lc");
            startInfo.ArgumentList.Add("command -v agent-up");
            return true;
        }

        if (command.Executable == SmokeExecutable.Bash && TryAddAllowedShellCommand(startInfo, command, "-lc", SelectUnixWorkingDirectoryCommand, out error))
            return true;

        if (command.Executable == SmokeExecutable.Ps && IsArguments(command, "-ef"))
        {
            startInfo.ArgumentList.Add("-ef");
            return true;
        }

        if (command.Executable == SmokeExecutable.Ps && IsArguments(command, "aux"))
        {
            startInfo.ArgumentList.Add("aux");
            return true;
        }

        if (command.Executable == SmokeExecutable.Ss && IsArguments(command, "-ltnp"))
        {
            startInfo.ArgumentList.Add("-ltnp");
            return true;
        }

        if (command.Executable == SmokeExecutable.Lsof && IsArguments(command, "-nP", "-iTCP", "-sTCP:LISTEN"))
        {
            startInfo.ArgumentList.Add("-nP");
            startInfo.ArgumentList.Add("-iTCP");
            startInfo.ArgumentList.Add("-sTCP:LISTEN");
            return true;
        }

        if (command.Executable == SmokeExecutable.Sudo)
            return TryAddSudoArguments(startInfo, command, out error);

        error = $"Command arguments are not allowed for {command.DisplayName}.";
        return false;
    }

    private static bool TryAddAllowedShellCommand(
        ProcessStartInfo startInfo,
        SafeCommandSpec command,
        string shellFlag,
        Func<string, string?> selectAllowedCommand,
        out string error)
    {
        error = "";
        if (command.Arguments.Count == 2 &&
            command.Arguments[0] == shellFlag &&
            selectAllowedCommand(command.Arguments[1]) is { } shellCommand)
        {
            startInfo.ArgumentList.Add(shellFlag);
            startInfo.ArgumentList.Add(shellCommand);
            return true;
        }

        error = "Shell command arguments are not allowed.";
        return false;
    }

    private static bool TryAddGitArguments(ProcessStartInfo startInfo, SafeCommandSpec command, out string error)
    {
        error = "";
        if (IsArguments(command, "init", "-q"))
        {
            startInfo.ArgumentList.Add("init");
            startInfo.ArgumentList.Add("-q");
            return true;
        }

        if (IsArguments(command, "config", "user.email", "smoke@ci.local"))
        {
            startInfo.ArgumentList.Add("config");
            startInfo.ArgumentList.Add("user.email");
            startInfo.ArgumentList.Add("smoke@ci.local");
            return true;
        }

        if (IsArguments(command, "config", "user.name", "Smoke CI"))
        {
            startInfo.ArgumentList.Add("config");
            startInfo.ArgumentList.Add("user.name");
            startInfo.ArgumentList.Add("Smoke CI");
            return true;
        }

        if (IsArguments(command, "add", "agent-up.json"))
        {
            startInfo.ArgumentList.Add("add");
            startInfo.ArgumentList.Add("agent-up.json");
            return true;
        }

        if (IsArguments(command, "commit", "-q", "-m", "Add service smoke workspace"))
        {
            startInfo.ArgumentList.Add("commit");
            startInfo.ArgumentList.Add("-q");
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add("Add service smoke workspace");
            return true;
        }

        error = "Git arguments are not allowed.";
        return false;
    }

    private static bool TryAddDpkgDebArguments(ProcessStartInfo startInfo, SafeCommandSpec command, out string error)
    {
        error = "";
        if (command.Arguments.Count == 3 && command.Arguments[0] is "-x" or "-e")
        {
            startInfo.ArgumentList.Add(command.Arguments[0]);
            startInfo.ArgumentList.Add(command.Arguments[1]);
            startInfo.ArgumentList.Add(command.Arguments[2]);
            return true;
        }

        error = "dpkg-deb arguments are not allowed.";
        return false;
    }

    private static bool TryAddPkgutilArguments(ProcessStartInfo startInfo, SafeCommandSpec command, out string error)
    {
        error = "";
        if (command.Arguments.Count == 3 && command.Arguments[0] == "--expand-full")
        {
            startInfo.ArgumentList.Add("--expand-full");
            startInfo.ArgumentList.Add(command.Arguments[1]);
            startInfo.ArgumentList.Add(command.Arguments[2]);
            return true;
        }

        error = "pkgutil arguments are not allowed.";
        return false;
    }

    private static bool TryAddMsiexecArguments(ProcessStartInfo startInfo, SafeCommandSpec command, out string error)
    {
        error = "";
        if (command.Arguments.Count == 6 &&
            command.Arguments[0] is "/i" or "/x" &&
            command.Arguments[2] == "/qn" &&
            command.Arguments[3] == "/norestart" &&
            command.Arguments[4] == "/l*vx!")
        {
            startInfo.ArgumentList.Add(command.Arguments[0]);
            startInfo.ArgumentList.Add(command.Arguments[1]);
            startInfo.ArgumentList.Add("/qn");
            startInfo.ArgumentList.Add("/norestart");
            startInfo.ArgumentList.Add("/l*vx!");
            startInfo.ArgumentList.Add(command.Arguments[5]);
            return true;
        }

        error = "msiexec arguments are not allowed.";
        return false;
    }

    private static bool TryAddPowerShellArguments(ProcessStartInfo startInfo, SafeCommandSpec command, out string error)
    {
        error = "";
        if (command.Arguments.Count == 3 &&
            command.Arguments[0] == "-NoProfile" &&
            command.Arguments[1] == "-Command" &&
            SelectWindowsPowerShellWorkingDirectoryCommand(command.Arguments[2]) is { } workingDirectoryCommand)
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(workingDirectoryCommand);
            return true;
        }

        if (IsArguments(command, "-NoProfile", "-Command", "$process = Start-Process -FilePath $env:AGENTUP_SMOKE_INSTALLER -ArgumentList @('/layout', $env:AGENTUP_SMOKE_LAYOUT, '/quiet') -Wait -PassThru; exit $process.ExitCode"))
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("$process = Start-Process -FilePath $env:AGENTUP_SMOKE_INSTALLER -ArgumentList @('/layout', $env:AGENTUP_SMOKE_LAYOUT, '/quiet') -Wait -PassThru; exit $process.ExitCode");
            return true;
        }

        if (IsArguments(command, "-NoProfile", "-Command", "Get-Service agent-up-server -ErrorAction SilentlyContinue | Format-List *"))
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("Get-Service agent-up-server -ErrorAction SilentlyContinue | Format-List *");
            return true;
        }

        if (IsArguments(command, "-NoProfile", "-Command", "$displayName = $env:AGENTUP_PRODUCT_DISPLAY_NAME; $installDir = [System.IO.Path]::GetFullPath($env:AGENTUP_INSTALL_DIR); $uninstallRoots = @('HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall', 'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall'); $registration = $uninstallRoots | Where-Object { Test-Path $_ } | ForEach-Object { Get-ChildItem $_ } | ForEach-Object { Get-ItemProperty $_.PSPath } | Where-Object { $_.DisplayName -eq $displayName -or $_.DisplayName -eq \"$displayName Setup\" } | Select-Object -First 1; if (-not $registration) { throw \"$displayName uninstall registration missing\" }; $path = [Environment]::GetEnvironmentVariable('Path', 'Machine'); $bin = [System.IO.Path]::GetFullPath((Join-Path $installDir 'bin')).TrimEnd('\\'); $entries = ($path -split ';' | Where-Object { $_ } | ForEach-Object { [System.IO.Path]::GetFullPath($_).TrimEnd('\\') }); if (-not ($entries | Where-Object { [string]::Equals($_, $bin, [System.StringComparison]::OrdinalIgnoreCase) })) { throw \"$displayName PATH entry missing: $bin\" }"))
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(command.Arguments[2]);
            return true;
        }

        error = "PowerShell arguments are not allowed.";
        return false;
    }

    private static bool TryAddSudoArguments(ProcessStartInfo startInfo, SafeCommandSpec command, out string error)
    {
        error = "";
        if (IsArguments(command, "apt-get", "purge", "-y", "agent-up"))
        {
            startInfo.ArgumentList.Add("apt-get");
            startInfo.ArgumentList.Add("purge");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("agent-up");
            return true;
        }

        if (IsArguments(command, "systemctl", "status", "agent-up-server.service", "--no-pager"))
        {
            startInfo.ArgumentList.Add("systemctl");
            startInfo.ArgumentList.Add("status");
            startInfo.ArgumentList.Add("agent-up-server.service");
            startInfo.ArgumentList.Add("--no-pager");
            return true;
        }

        if (IsArguments(command, "journalctl", "-u", "agent-up-server.service", "--no-pager", "-n", "200"))
        {
            startInfo.ArgumentList.Add("journalctl");
            startInfo.ArgumentList.Add("-u");
            startInfo.ArgumentList.Add("agent-up-server.service");
            startInfo.ArgumentList.Add("--no-pager");
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add("200");
            return true;
        }

        if (IsArguments(command, "tail", "-n", "200", "/var/log/agent-up-server.log"))
        {
            startInfo.ArgumentList.Add("tail");
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add("200");
            startInfo.ArgumentList.Add("/var/log/agent-up-server.log");
            return true;
        }

        if (IsArguments(command, "tail", "-n", "200", "/var/log/agent-up-server.err.log"))
        {
            startInfo.ArgumentList.Add("tail");
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add("200");
            startInfo.ArgumentList.Add("/var/log/agent-up-server.err.log");
            return true;
        }

        if (IsArguments(command, "ls", "-la", "/var/lib/agent-up"))
        {
            startInfo.ArgumentList.Add("ls");
            startInfo.ArgumentList.Add("-la");
            startInfo.ArgumentList.Add("/var/lib/agent-up");
            return true;
        }

        if (IsArguments(command, "launchctl", "print", "system/dev.agent-up.server"))
        {
            startInfo.ArgumentList.Add("launchctl");
            startInfo.ArgumentList.Add("print");
            startInfo.ArgumentList.Add("system/dev.agent-up.server");
            return true;
        }

        if (IsArguments(command, "tail", "-n", "200", "/Library/Logs/Agent-Up/server.out.log"))
        {
            startInfo.ArgumentList.Add("tail");
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add("200");
            startInfo.ArgumentList.Add("/Library/Logs/Agent-Up/server.out.log");
            return true;
        }

        if (IsArguments(command, "tail", "-n", "200", "/Library/Logs/Agent-Up/server.err.log"))
        {
            startInfo.ArgumentList.Add("tail");
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add("200");
            startInfo.ArgumentList.Add("/Library/Logs/Agent-Up/server.err.log");
            return true;
        }

        if (IsArguments(command, "ls", "-la", "/Library/Application Support/Agent-Up"))
        {
            startInfo.ArgumentList.Add("ls");
            startInfo.ArgumentList.Add("-la");
            startInfo.ArgumentList.Add("/Library/Application Support/Agent-Up");
            return true;
        }

        if (command.Arguments.Count == 5 &&
            command.Arguments[0] == "installer" &&
            command.Arguments[1] == "-pkg" &&
            command.Arguments[3] == "-target" &&
            command.Arguments[4] == "/")
        {
            startInfo.ArgumentList.Add("installer");
            startInfo.ArgumentList.Add("-pkg");
            startInfo.ArgumentList.Add(command.Arguments[2]);
            startInfo.ArgumentList.Add("-target");
            startInfo.ArgumentList.Add("/");
            return true;
        }

        if (command.Arguments.Count == 4 && command.Arguments[0] == "apt-get" && command.Arguments[1] == "install" && command.Arguments[2] == "-y")
        {
            startInfo.ArgumentList.Add("apt-get");
            startInfo.ArgumentList.Add("install");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add(command.Arguments[3]);
            return true;
        }

        if (command.Arguments.Count == 3 && command.Arguments[0] == "bash" && command.Arguments[1] == "-c")
        {
            startInfo.ArgumentList.Add("bash");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command.Arguments[2]);
            return true;
        }

        error = "sudo arguments are not allowed.";
        return false;
    }

    private static bool IsArguments(SafeCommandSpec command, params string[] arguments)
        => command.Arguments.Count == arguments.Length && command.Arguments.SequenceEqual(arguments);

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

        if (workingDirectory is not null)
        {
            environment ??= [];
            if (environment.ContainsKey(WorkingDirectoryEnvironmentKey))
            {
                error = $"Environment variable name '{WorkingDirectoryEnvironmentKey}' is reserved.";
                return false;
            }

            environment.Add(WorkingDirectoryEnvironmentKey, workingDirectory);
        }

        safeCommand = new SafeCommandSpec(executable, DisplayName(executable), arguments, environment);
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

    private static bool TryNormalizeWorkingDirectory(string? workingDirectory, out string? safeWorkingDirectory, out string error)
    {
        if (workingDirectory is null)
        {
            safeWorkingDirectory = null;
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

        return key.All(character => character == '_' || char.IsAsciiLetterOrDigit(character));
    }

    private static bool TryGetAllowedCommandName(string fileName, out SmokeExecutable executable, out string error)
    {
        executable = fileName switch
        {
            "agent-up" => SmokeExecutable.AgentUp,
            "agent-up.cmd" => SmokeExecutable.AgentUpCmd,
            "bash" => SmokeExecutable.Bash,
            "cmd.exe" => SmokeExecutable.Cmd,
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
            SmokeExecutable.Cmd => "cmd.exe",
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

    private const string WorkingDirectoryEnvironmentKey = "AGENTUP_SMOKE_WORKING_DIRECTORY";

    private static string? SelectUnixWorkingDirectoryCommand(string command)
        => command switch
        {
            "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && agent-up start" => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && agent-up start",
            "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && agent-up status" => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && agent-up status",
            "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git init -q" => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git init -q",
            "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git config user.email smoke@ci.local" => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git config user.email smoke@ci.local",
            "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git config user.name \"Smoke CI\"" => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git config user.name \"Smoke CI\"",
            "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git add agent-up.json" => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git add agent-up.json",
            "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git commit -q -m \"Add service smoke workspace\"" => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git commit -q -m \"Add service smoke workspace\"",
            _ => null
        };

    private static string? SelectWindowsPowerShellWorkingDirectoryCommand(string command)
        => command switch
        {
            "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; agent-up.cmd start" => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; agent-up.cmd start",
            "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; agent-up.cmd status" => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; agent-up.cmd status",
            "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git init -q" => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git init -q",
            "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git config user.email smoke@ci.local" => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git config user.email smoke@ci.local",
            "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git config user.name \"Smoke CI\"" => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git config user.name \"Smoke CI\"",
            "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git add agent-up.json" => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git add agent-up.json",
            "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git commit -q -m \"Add service smoke workspace\"" => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git commit -q -m \"Add service smoke workspace\"",
            _ => null
        };
}
