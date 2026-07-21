using System.Diagnostics;
using System.Runtime.InteropServices;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Processes.Providers;

public sealed class LocalProcessProvider : ILocalProcessProvider
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
        foreach (var mapping in workspace.Applications.SelectMany(a => a.AllocatedPorts).Where(mapping => mapping.Variable is not null))
            startInfo.Environment[mapping.Variable!] = mapping.AllocatedPort.ToString();

        return startInfo;
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
}
