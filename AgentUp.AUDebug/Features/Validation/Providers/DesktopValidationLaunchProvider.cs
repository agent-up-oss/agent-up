using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Features.Validation.DTOs;
using AgentUp.AUDebug.Features.Validation.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Validation.Providers;

public sealed class DesktopValidationLaunchProvider(
    IAllowlistedProcessRunner processes,
    IDebugPathValidator paths,
    IDebugEnvironment environment,
    IHostReadyProbe probe,
    IValidationWorkspaceFixture fixture,
    ILoopbackEndpointOccupancy occupancy) : IDesktopValidationLaunch
{
    private readonly object _logGate = new();

    public string ResolveOutputPath(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return paths.JoinUnderRoot("artifacts", "desktop-validation");
        if (Path.IsPathRooted(outputPath))
            return Path.GetFullPath(outputPath);
        return paths.JoinUnderRoot(outputPath);
    }

    public async Task<ProcessResult> RunAsync(
        DesktopValidationLaunchSpec spec,
        CancellationToken cancellationToken)
    {
        if (await occupancy.IsOccupiedAsync(DebugLayout.ServerUrl, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Isolated Desktop validation needs {DebugLayout.ServerUrl} free. Something is already listening there.");
        }

        Directory.CreateDirectory(spec.OutputPath);
        var logPath = Path.Join(spec.OutputPath, "server.log");
        var dataDirectory = fixture.CreateDataDirectory();
        var process = processes.Start(ServerCommand(dataDirectory, fixture.ServerEnvironment(dataDirectory)));
        AttachServerLog(process, logPath);
        try
        {
            await probe.WaitAsync(DebugLayout.ServerUrl + DebugLayout.ServerReadyPath, cancellationToken);
            await fixture.PrepareAsync(DebugLayout.ServerUrl, cancellationToken);
            return await processes.RunAsync(DesktopCommand(spec, logPath), cancellationToken);
        }
        finally
        {
            processes.KillTree(process.Id);
        }
    }

    private AllowlistedCommand ServerCommand(string dataDirectory, IReadOnlyDictionary<string, string> extra)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = DebugLayout.ServerUrl,
            ["AGENTUP_AUTH_DISABLED"] = "true",
            ["AGENTUP_SKIP_DEV_AGENT_BOOTSTRAP"] = "1",
            ["Storage__DataDirectory"] = dataDirectory
        };
        foreach (var pair in extra)
            environment[pair.Key] = pair.Value;
        return new(
            "dotnet",
            ["run", "--project", paths.JoinUnderRoot("AgentUp.Server"), "--no-launch-profile", "--urls", DebugLayout.ServerUrl],
            paths.RepositoryRoot,
            environment);
    }

    private AllowlistedCommand DesktopCommand(DesktopValidationLaunchSpec spec, string logPath)
    {
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AGENTUP_SERVER_URL"] = DebugLayout.ServerUrl,
            ["DISPLAY"] = environment.Display,
            [DesktopValidationLaunchKeys.Surface] = DesktopValidationLaunchKeys.DesktopValue,
            [DesktopValidationLaunchKeys.Output] = spec.OutputPath,
            [DesktopValidationLaunchKeys.SkipCosmetic] = "1",
            [DesktopValidationLaunchKeys.ServerLog] = logPath
        };
        if (!string.IsNullOrWhiteSpace(spec.FlowId))
            environmentVariables[DesktopValidationLaunchKeys.Flow] = spec.FlowId;
        if (!string.IsNullOrWhiteSpace(spec.WorkspaceId))
            environmentVariables[DesktopValidationLaunchKeys.Workspace] = spec.WorkspaceId;
        if (!string.IsNullOrWhiteSpace(spec.Password))
            environmentVariables["AGENTUP_ADMIN_PASSWORD"] = spec.Password;

        return new(
            "dotnet",
            ["run", "--project", paths.JoinUnderRoot("AgentUp.Desktop"), "--no-launch-profile"],
            paths.RepositoryRoot,
            environmentVariables);
    }

    private void AttachServerLog(System.Diagnostics.Process process, string logPath)
    {
        try
        {
            process.OutputDataReceived += (_, args) => AppendLog(logPath, args.Data);
            process.ErrorDataReceived += (_, args) => AppendLog(logPath, args.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (InvalidOperationException exception)
        {
            System.Diagnostics.Trace.WriteLine(exception.Message);
        }
    }

    private void AppendLog(string logPath, string? line)
    {
        if (string.IsNullOrEmpty(line))
            return;
        try
        {
            lock (_logGate)
                File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine(exception.Message);
        }
    }
}
