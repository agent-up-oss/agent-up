using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Processes.Providers;

public sealed partial class LocalProcessProvider : ILocalProcessProvider
{
    public Process CreateApplicationProcess(Workspace workspace, ApplicationInstance app)
        => new()
        {
            StartInfo = CreateStartInfo(workspace, app),
            EnableRaisingEvents = true
        };

    public void Kill(Process process)
        => process.Kill(entireProcessTree: true);

    internal static ProcessStartInfo CreateStartInfo(Workspace workspace, ApplicationInstance app)
    {
        var workingDirectory = WorkspacePathProvider.ResolveWorkspacePath(
            workspace.WorktreePath,
            app.Path,
            "Application path");
        var fileEnvironment = LoadEnvironmentFiles(workspace.WorktreePath, app.EnvironmentFiles);
        var startInfo = CreateShellStartInfo(app.Command!, workingDirectory);
        foreach (var (key, value) in fileEnvironment)
            startInfo.Environment[key] = value;

        foreach (var (key, value) in app.Environment ?? new Dictionary<string, string>())
            startInfo.Environment[key] = value;

        foreach (var mapping in workspace.Applications.SelectMany(a => a.AllocatedPorts).Where(mapping => mapping.Variable is not null))
            startInfo.Environment[mapping.Variable!] = mapping.AllocatedPort.ToString();

        return startInfo;
    }

    private static Dictionary<string, string> LoadEnvironmentFiles(string worktreePath, IReadOnlyList<string>? environmentFiles)
    {
        var environment = new Dictionary<string, string>();
        foreach (var environmentFile in environmentFiles ?? [])
        {
            var lines = EnvironmentFilePathProvider.ReadExistingWorkspaceFileLines(worktreePath, environmentFile);
            foreach (var (key, value) in ParseEnvironmentFile(lines, environmentFile))
                environment[key] = value;
        }

        return environment;
    }

    internal static Dictionary<string, string> ParseEnvironmentFile(IEnumerable<string> lines, string sourceName)
    {
        var environment = new Dictionary<string, string>();
        var lineNumber = 0;
        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                throw new InvalidOperationException($"Environment file '{sourceName}' has an invalid entry on line {lineNumber}.");

            var key = line[..equalsIndex].Trim();
            if (!EnvironmentVariableName().IsMatch(key))
                throw new InvalidOperationException($"Environment file '{sourceName}' has an invalid variable name on line {lineNumber}.");

            var value = line[(equalsIndex + 1)..].Trim();
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            environment[key] = value;
        }

        return environment;
    }

    private static ProcessStartInfo CreateShellStartInfo(string command, string workingDirectory)
    {
        var scriptPath = WriteCommandScript(command, workingDirectory);
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.Environment["AGENTUP_COMMAND_SCRIPT"] = scriptPath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/D");
            startInfo.ArgumentList.Add("/Q");
            startInfo.ArgumentList.Add("/C");
            startInfo.ArgumentList.Add("\"%AGENTUP_COMMAND_SCRIPT%\"");
        }
        else
        {
            startInfo.FileName = "/usr/bin/env";
            startInfo.ArgumentList.Add("bash");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("exec \"$AGENTUP_COMMAND_SCRIPT\"");
        }

        return startInfo;
    }

    private static string WriteCommandScript(string command, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("Application command must not be empty.");

        var runtimeDirectory = WorkspacePathProvider.ResolveWorkspacePath(
            workingDirectory,
            Path.Join(".agent-up", "runtime", "commands"),
            "Application runtime path");
        Directory.CreateDirectory(runtimeDirectory);
        var scriptName = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(command)))[..16]
                         + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : ".sh");
        var scriptPath = WorkspacePathProvider.ResolveWorkspacePath(runtimeDirectory, scriptName, "Application command script path");
        if (!File.Exists(scriptPath))
        {
            var tempPath = scriptPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempPath, command);
            File.Move(tempPath, scriptPath);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return scriptPath;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentVariableName();
}
