using System.Diagnostics;
using System.Text.RegularExpressions;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;

namespace AgentUp.Server.Features.Processes.Providers;

public sealed partial class DockerProcessProvider : IDockerProcessProvider
{
    public string GetContainerName(string workspaceId, string appName)
    {
        var safeId = workspaceId[..Math.Min(8, workspaceId.Length)];
        var safeName = ContainerNameSanitizer().Replace(appName.ToLower(), "-").Trim('-');
        return $"agentup-{safeId}-{safeName}";
    }

    public IReadOnlyList<string> CreateRunArguments(string containerName, ApplicationInstance app, string worktreePath)
    {
        var runArgs = new List<string> { "run", "-d", "--name", containerName };
        AddDockerPortArgs(runArgs, app);
        AddDockerEnvironmentFileArgs(runArgs, app, worktreePath);
        AddDockerEnvironmentArgs(runArgs, app);
        AddDockerVolumeArgs(runArgs, app);
        runArgs.Add(app.Image!);
        return runArgs;
    }

    public Process CreateLogProcess(string containerName)
    {
        var logProcess = new Process
        {
            StartInfo = CreateDockerStartInfo(),
            EnableRaisingEvents = true
        };
        logProcess.StartInfo.ArgumentList.Add("logs");
        logProcess.StartInfo.ArgumentList.Add("-f");
        logProcess.StartInfo.ArgumentList.Add(containerName);
        return logProcess;
    }

    public async Task<DockerCommandResult> RunAsync(params string[] args)
    {
        using var process = new Process
        {
            StartInfo = CreateDockerStartInfo()
        };
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"docker could not be started: {ex.Message}", ex);
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new DockerCommandResult(process.ExitCode, stdout, stderr);
    }

    public async Task<int> GetExitCodeAsync(string containerName)
    {
        var result = await RunAsync("inspect", "--format={{.State.ExitCode}}", containerName);
        return int.TryParse(result.Stdout.Trim(), out var code) ? code : 1;
    }

    private static ProcessStartInfo CreateDockerStartInfo()
        => new()
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

    private static void AddDockerPortArgs(List<string> runArgs, ApplicationInstance app)
    {
        foreach (var mapping in app.AllocatedPorts)
        {
            runArgs.Add("-p");
            runArgs.Add($"{mapping.AllocatedPort}:{mapping.DefaultPort}");
        }
    }

    private static void AddDockerEnvironmentArgs(List<string> runArgs, ApplicationInstance app)
    {
        foreach (var (key, value) in app.Environment ?? new Dictionary<string, string>())
        {
            runArgs.Add("-e");
            runArgs.Add($"{key}={value}");
        }
    }

    private static void AddDockerEnvironmentFileArgs(List<string> runArgs, ApplicationInstance app, string worktreePath)
    {
        foreach (var environmentFile in app.EnvironmentFiles ?? [])
        {
            var fullPath = ResolveEnvironmentFilePath(worktreePath, environmentFile);
            if (!File.Exists(fullPath))
                throw new InvalidOperationException($"Environment file '{environmentFile}' was not found.");

            runArgs.Add("--env-file");
            runArgs.Add(fullPath);
        }
    }

    private static string ResolveEnvironmentFilePath(string worktreePath, string environmentFile)
    {
        if (string.IsNullOrWhiteSpace(environmentFile))
            throw new InvalidOperationException("Environment file paths must not be empty.");

        if (Path.IsPathRooted(environmentFile))
            throw new InvalidOperationException($"Environment file '{environmentFile}' must be relative to the workspace root.");

        var root = Path.GetFullPath(worktreePath);
        var fullPath = Path.GetFullPath(Path.Join(root, environmentFile));
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal))
            throw new InvalidOperationException($"Environment file '{environmentFile}' must stay under the workspace root.");

        return fullPath;
    }

    private static void AddDockerVolumeArgs(List<string> runArgs, ApplicationInstance app)
    {
        foreach (var volume in app.Volumes ?? [])
        {
            runArgs.Add("-v");
            runArgs.Add(volume);
        }
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex ContainerNameSanitizer();
}
