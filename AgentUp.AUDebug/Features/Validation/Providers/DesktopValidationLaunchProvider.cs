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
    IHostReadyProbe probe) : IDesktopValidationLaunch
{
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
        var startedServer = await EnsureServerAsync(cancellationToken);
        try
        {
            return await processes.RunAsync(DesktopCommand(spec), cancellationToken);
        }
        finally
        {
            if (startedServer is { } process)
                processes.KillTree(process.Id);
        }
    }

    private async Task<System.Diagnostics.Process?> EnsureServerAsync(CancellationToken cancellationToken)
    {
        var readyUrl = DebugLayout.ServerUrl + DebugLayout.ServerReadyPath;
        if (await probe.CheckAsync(readyUrl, cancellationToken))
            return null;

        var process = processes.Start(ServerCommand());
        await probe.WaitAsync(readyUrl, cancellationToken);
        return process;
    }

    private AllowlistedCommand ServerCommand()
        => new(
            "dotnet",
            ["run", "--project", paths.JoinUnderRoot("AgentUp.Server"), "--urls", DebugLayout.ServerUrl],
            paths.RepositoryRoot,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ASPNETCORE_URLS"] = DebugLayout.ServerUrl
            });

    private AllowlistedCommand DesktopCommand(DesktopValidationLaunchSpec spec)
    {
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AGENTUP_SERVER_URL"] = DebugLayout.ServerUrl,
            ["DISPLAY"] = environment.Display,
            [DesktopValidationLaunchKeys.Surface] = DesktopValidationLaunchKeys.DesktopValue,
            [DesktopValidationLaunchKeys.Output] = spec.OutputPath,
            [DesktopValidationLaunchKeys.SkipCosmetic] = "1",
            [DesktopValidationLaunchKeys.ServerLog] = Path.Join(paths.LogsDirectory, "server.log")
        };
        if (!string.IsNullOrWhiteSpace(spec.FlowId))
            environmentVariables[DesktopValidationLaunchKeys.Flow] = spec.FlowId;
        if (!string.IsNullOrWhiteSpace(spec.WorkspaceId))
            environmentVariables[DesktopValidationLaunchKeys.Workspace] = spec.WorkspaceId;
        if (!string.IsNullOrWhiteSpace(spec.Password))
            environmentVariables["AGENTUP_ADMIN_PASSWORD"] = spec.Password;

        return new(
            "bash",
            [paths.JoinUnderRoot("run-desktop.sh")],
            paths.RepositoryRoot,
            environmentVariables);
    }
}
