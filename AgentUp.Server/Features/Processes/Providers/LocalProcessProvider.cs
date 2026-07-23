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
        var workingDirectory = ResolveWorkspacePath(workspace.WorktreePath, app.Path);
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
            var fullPath = EnvironmentFilePathProvider.ResolveExistingWorkspaceFile(worktreePath, environmentFile);
            foreach (var (key, value) in ParseEnvironmentFile(File.ReadLines(fullPath), environmentFile))
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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/D");
            startInfo.ArgumentList.Add("/Q");
            startInfo.ArgumentList.Add("/C");
            startInfo.ArgumentList.Add(scriptPath);
        }
        else
        {
            startInfo.FileName = "/usr/bin/env";
            startInfo.ArgumentList.Add("bash");
            startInfo.ArgumentList.Add(scriptPath);
        }

        return startInfo;
    }

    private static string WriteCommandScript(string command, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("Application command must not be empty.");

        var runtimeDirectory = ResolveWorkspacePath(workingDirectory, Path.Join(".agent-up", "runtime", "commands"));
        Directory.CreateDirectory(runtimeDirectory);
        var scriptName = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(command)))[..16]
                         + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : ".sh");
        var scriptPath = ResolveWorkspacePath(runtimeDirectory, scriptName);
        File.WriteAllText(scriptPath, command);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return scriptPath;
    }

    private static string ResolveWorkspacePath(string root, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Workspace path must not be empty.");

        var rootFullPath = Path.GetFullPath(root);
        if (string.IsNullOrWhiteSpace(relativePath))
            return rootFullPath;

        if (Path.IsPathRooted(relativePath))
            throw new InvalidOperationException("Application path must be relative to the workspace root.");

        var fullPath = Path.GetFullPath(Path.Join(rootFullPath, relativePath));
        var relative = Path.GetRelativePath(rootFullPath, fullPath);
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal))
            throw new InvalidOperationException("Application path must stay under the workspace root.");

        return fullPath;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentVariableName();
}
