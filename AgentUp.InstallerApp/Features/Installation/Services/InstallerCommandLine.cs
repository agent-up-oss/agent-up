using AgentUp.InstallerApp.Features.Logging.Tools;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;

namespace AgentUp.InstallerApp.Features.Installation.Services;

public static class InstallerCommandLine
{
    public const string InstallCoreArgument = "--install-core";
    public const string SmokeInstallerOperationsArgument = "--smoke-installer-operations";
    public const string ValidateInstalledArgument = "--validate-installed";
    public const string InstallComponentArgument = "--install-component";
    public const string UpdateComponentArgument = "--update-component";
    public const string RepairComponentArgument = "--repair-component";
    public const string UninstallComponentArgument = "--uninstall-component";

    private static readonly string[] CommandArguments =
    [
        InstallCoreArgument,
        SmokeInstallerOperationsArgument,
        ValidateInstalledArgument,
        InstallComponentArgument,
        UpdateComponentArgument,
        RepairComponentArgument,
        UninstallComponentArgument
    ];

    public static bool ShouldRunCommandLine(string[] args)
        => args.Any(arg => CommandArguments.Contains(arg, StringComparer.OrdinalIgnoreCase));

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        InstallerLog.Write($"CLI: args=[{string.Join(", ", args)}]");
        try
        {
            var adapter = InstallerPlatformAdapterFactory.Create();
            return await RunAsync(adapter, args, output, error, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            InstallerLog.WriteException("CLI", exception);
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunAsync(
        IInstallerPlatformAdapter adapter,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        if (args.Contains(SmokeInstallerOperationsArgument, StringComparer.OrdinalIgnoreCase))
            return await RunSmokeInstallerOperationsAsync(adapter, output, error, cancellationToken);
        if (args.Contains(ValidateInstalledArgument, StringComparer.OrdinalIgnoreCase))
            return await RunValidateInstalledAsync(adapter, output, error, cancellationToken);
        if (TryComponentAction(args, InstallComponentArgument, out var installTarget))
            return await RunComponentActionAsync(adapter, installTarget, InstallerComponentAction.Install, output, error, cancellationToken);
        if (TryComponentAction(args, UpdateComponentArgument, out var updateTarget))
            return await RunComponentActionAsync(adapter, updateTarget, InstallerComponentAction.Update, output, error, cancellationToken);
        if (TryComponentAction(args, RepairComponentArgument, out var repairTarget))
            return await RunComponentActionAsync(adapter, repairTarget, InstallerComponentAction.Repair, output, error, cancellationToken);
        if (TryComponentAction(args, UninstallComponentArgument, out var uninstallTarget))
            return await RunComponentActionAsync(adapter, uninstallTarget, InstallerComponentAction.Uninstall, output, error, cancellationToken);
        if (args.Contains(InstallCoreArgument, StringComparer.OrdinalIgnoreCase))
            return await RunInstallCoreAsync(adapter, output, error, cancellationToken);

        await error.WriteLineAsync("No installer command was provided.");
        return 2;
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

        var session = CreateSession();
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

    private static async Task<int> RunSmokeInstallerOperationsAsync(
        IInstallerPlatformAdapter adapter,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        foreach (var component in AgentUpManifest().Components)
        {
            foreach (var action in new[] { InstallerComponentAction.Install, InstallerComponentAction.Repair, InstallerComponentAction.Update, InstallerComponentAction.Uninstall })
            {
                var exitCode = await RunComponentActionAsync(adapter, component, action, output, error, cancellationToken);
                if (exitCode != 0)
                    return exitCode;
            }
        }

        var coreExitCode = await RunInstallCoreAsync(adapter, output, error, cancellationToken);
        if (coreExitCode != 0)
            return coreExitCode;

        coreExitCode = await RunValidateInstalledAsync(adapter, output, error, cancellationToken);
        if (coreExitCode != 0)
            return coreExitCode;

        await output.WriteLineAsync("Installer operation smoke succeeded.");
        return 0;
    }

    private static async Task<int> RunComponentActionAsync(
        IInstallerPlatformAdapter adapter,
        ProductComponent component,
        InstallerComponentAction action,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!adapter.SupportsInstallActions)
        {
            await error.WriteLineAsync($"{adapter.PlatformName} does not support installer-managed component actions.");
            return 1;
        }

        var session = CreateSession();
        await foreach (var progress in adapter.ExecuteComponentActionAsync(component, action, session, cancellationToken))
            await output.WriteLineAsync($"{component.DisplayName} {action}: {progress.CompletedOperations}/{progress.TotalOperations}: {progress.Message}");

        var status = await adapter.GetComponentStatusAsync(component, session, cancellationToken);
        var expected = action == InstallerComponentAction.Uninstall
            ? InstallerComponentStatusKind.NotInstalled
            : InstallerComponentStatusKind.Installed;

        if (status.Kind == expected || action != InstallerComponentAction.Uninstall && status.Kind == InstallerComponentStatusKind.UpdateAvailable)
        {
            await output.WriteLineAsync($"{component.DisplayName} {action} succeeded.");
            return 0;
        }

        await error.WriteLineAsync($"{component.DisplayName} {action} expected {expected}, got {status.Kind}: {status.Message}");
        return 1;
    }

    private static async Task<int> RunValidateInstalledAsync(
        IInstallerPlatformAdapter adapter,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var report = await adapter.ValidateInstalledStateAsync(CreateSession(), cancellationToken);
        foreach (var finding in report.Findings)
            await output.WriteLineAsync($"{finding.Severity}: {finding.Code} {finding.Message}");

        if (report.Succeeded)
        {
            await output.WriteLineAsync("Installed state validation succeeded.");
            return 0;
        }

        await error.WriteLineAsync("Installed state validation failed.");
        return 1;
    }

    private static bool TryComponentAction(string[] args, string argument, out ProductComponent component)
    {
        component = ProductComponent.Desktop;
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].Equals(argument, StringComparison.OrdinalIgnoreCase))
                continue;
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                throw new InvalidOperationException($"{argument} requires a component target.");

            component = ParseComponent(args[index + 1]);
            return true;
        }

        return false;
    }

    private static ProductComponent ParseComponent(string value)
        => value.ToLowerInvariant() switch
        {
            "desktop" => ProductComponent.Desktop,
            "server" => ProductComponent.Server,
            "cli" => ProductComponent.Cli,
            _ => throw new InvalidOperationException($"Unknown installer component '{value}'. Expected desktop, server, or cli.")
        };

    private static InstallerSession CreateSession()
    {
        var version = InstallerVersion();
        var manifest = AgentUpManifest();
        return InstallerSession.CreateDefault(
            manifest,
            version,
            manifest.DefaultInstallRoot(),
            PayloadSelection.Bundled(version));
    }

    private static Version InstallerVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v is null || v == new Version(0, 0, 0, 0) ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, v.Build);
    }

    private static ProductManifest AgentUpManifest()
        => ProductManifest.AgentUp();
}
