using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;

namespace AgentUp.InstallerApp.Features.Installation.Services;

public static class InstallerCommandLine
{
    public const string InstallCoreArgument = "--install-core";

    public static bool ShouldRunInstallCore(string[] args)
        => args.Contains(InstallCoreArgument, StringComparer.OrdinalIgnoreCase);

    public static async Task<int> RunInstallCoreAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var adapter = InstallerPlatformAdapterFactory.Create();
            return await RunInstallCoreAsync(adapter, output, error, cancellationToken);
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunInstallCoreAsync(
        IInstallerPlatformAdapter adapter,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        if (!adapter.SupportsInstallActions)
        {
            await error.WriteLineAsync($"{adapter.PlatformName} does not support installer-managed core app installation.");
            return 1;
        }

        var version = new Version(0, 0, 0);
        var session = InstallerSession.CreateDefault(
            "Agent-Up",
            version,
            DefaultInstallRoot(),
            PayloadSelection.Bundled(version));

        var docker = await adapter.CheckDockerAsync(cancellationToken);
        session = InstallerWorkflow.WithDockerStatus(session, docker);
        session = InstallerWorkflow.StartInstall(session);

        await foreach (var progress in adapter.ExecuteInstallAsync(session, cancellationToken))
            await output.WriteLineAsync($"{progress.CompletedOperations}/{progress.TotalOperations}: {progress.Message}");

        var report = await adapter.ValidateInstalledStateAsync(session, cancellationToken);
        foreach (var finding in report.Findings)
            await output.WriteLineAsync($"{finding.Severity}: {finding.Code} {finding.Message}");

        if (report.Succeeded)
        {
            await output.WriteLineAsync("Core app installation succeeded.");
            return 0;
        }

        await error.WriteLineAsync("Core app installation failed validation.");
        return 1;
    }

    private static string DefaultInstallRoot()
    {
        if (OperatingSystem.IsWindows())
            return @"C:\Program Files\Agent-Up";
        if (OperatingSystem.IsMacOS())
            return "/Applications/Agent-Up.app";
        return "/opt/agent-up";
    }
}
