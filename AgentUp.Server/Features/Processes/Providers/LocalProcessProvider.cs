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
        var workingDirectory = app.Path is not null
            ? Path.Join(workspace.WorktreePath, app.Path)
            : workspace.WorktreePath;
        var startInfo = CreateShellStartInfo(app.Command!, workingDirectory);
        foreach (var (key, value) in LoadEnvironmentFiles(workspace.WorktreePath, app.EnvironmentFiles))
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
            startInfo.ArgumentList.Add("/C");
        }
        else
        {
            startInfo.FileName = "/usr/bin/env";
            startInfo.ArgumentList.Add("bash");
            startInfo.ArgumentList.Add("-c");
        }

        startInfo.ArgumentList.Add(command);
        return startInfo;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentVariableName();
}
