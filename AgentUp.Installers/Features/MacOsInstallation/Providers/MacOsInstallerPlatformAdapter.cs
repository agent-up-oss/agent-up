using System.Text;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.MacOsInstallation.DTOs;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Features.MacOsInstallation.Providers;

public sealed class MacOsInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    private readonly ICommandRunner _commands;
    private readonly IMacOsInstallerFileSystem _files;
    private readonly MacOsInstallerOptions _options;
    private readonly IRequiredCommandRunner _requiredCommands;
    private readonly DockerPrerequisite _dockerPrerequisite;

    public MacOsInstallerPlatformAdapter(
        ICommandRunner commands,
        IMacOsInstallerFileSystem files,
        MacOsInstallerOptions options,
        IRequiredCommandRunner requiredCommands,
        DockerPrerequisite dockerPrerequisite)
    {
        _commands = commands;
        _files = files;
        _options = options;
        _requiredCommands = requiredCommands;
        _dockerPrerequisite = dockerPrerequisite;
    }

    public string PlatformName => "macOS";

    public bool SupportsInstallActions => true;

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await _dockerPrerequisite.CheckAsync(cancellationToken);

    public async Task<InstallerComponentStatus> GetComponentStatusAsync(
        ProductComponent component,
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => InstallerComponentOperations.StatusFromValidation(
            component,
            await ValidateInstalledStateAsync(session, cancellationToken),
            session.Version);

    public IReadOnlyList<InstallOperation> PlanComponentAction(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session)
        => InstallerComponentOperations.Plan(TargetFor(component), action, session, PlanInstall);

    public IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => InstallerComponentOperations.ExecuteInstallLikeAction(
            TargetFor(component),
            action,
            session,
            ExecuteInstallAsync,
            ExecuteUninstallAsync,
            cancellationToken);

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
    {
        var summary = session.Summary();
        var operations = new List<InstallOperation>
        {
            new(InstallOperationKind.ValidatePrerequisites, "Validate Docker and macOS prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Use {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, "Install selected Agent-Up files", true)
        };

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterService, "Register and start dev.agent-up.server launchd service", true));
        if (summary.Includes(InstallerComponent.Cli))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterCli, "Register /usr/local/bin Agent-Up commands", true));
        if (summary.Includes(InstallerComponent.Desktop))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterDesktop, "Register Agent-Up.app in /Applications", true));

        operations.Add(new InstallOperation(InstallOperationKind.RegisterUninstall, "Register native uninstall handoff", true));
        operations.Add(new InstallOperation(InstallOperationKind.ValidateInstallation, "Validate macOS installed state", false));
        return operations;
    }

    public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = PlanInstall(session);
        var progress = new InstallProgressTracker(operations);
        var manifest = MacOsInstallerManifest.Create(session.Version.ToString());
        var plists = new MacOsInstallerPlistGenerator(manifest);

        await _commands.RunAsync("launchctl", $"bootout system {_options.Paths.LaunchDaemonPath}", cancellationToken);
        yield return progress.Complete(InstallOperationKind.ValidatePrerequisites);
        yield return progress.Complete(InstallOperationKind.StagePayload);

        var summary = session.Summary();
        var tempFiles = new Dictionary<string, string>();
        try
        {
            if (summary.Includes(InstallerComponent.Desktop))
            {
                var tmpPlist = Path.Combine("/tmp", Path.GetRandomFileName());
                await File.WriteAllTextAsync(tmpPlist, plists.DesktopInfoPlist(), cancellationToken);
                tempFiles[_options.Paths.DesktopInfoPlistPath] = tmpPlist;
            }

            if (summary.Includes(InstallerComponent.Server))
            {
                var tmpDaemon = Path.Combine("/tmp", Path.GetRandomFileName());
                await File.WriteAllTextAsync(tmpDaemon, plists.LaunchDaemonPlist(), cancellationToken);
                tempFiles[_options.Paths.LaunchDaemonPath] = tmpDaemon;
            }

            await RunElevatedAsync(BuildInstallScript(session, summary, tempFiles, manifest), cancellationToken);
        }
        finally
        {
            foreach (var tmp in tempFiles.Values)
            {
                try { File.Delete(tmp); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        yield return progress.Complete(InstallOperationKind.InstallFiles);

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
            yield return progress.Complete(InstallOperationKind.RegisterService);

        if (summary.Includes(InstallerComponent.Cli))
            yield return progress.Complete(InstallOperationKind.RegisterCli);

        if (summary.Includes(InstallerComponent.Desktop))
            yield return progress.Complete(InstallOperationKind.RegisterDesktop);

        yield return progress.Complete(InstallOperationKind.RegisterUninstall);
        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    public async IAsyncEnumerable<InstallProgress> ExecuteUninstallAsync(
        InstallerComponentTarget target,
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = InstallerComponentOperations.Plan(target, InstallerComponentAction.Uninstall, session, PlanInstall);
        var progress = new InstallProgressTracker(operations);

        await RunElevatedAsync(BuildUninstallScript(target), cancellationToken);

        yield return progress.Complete(InstallerComponentOperations.TargetOperationKind(target));
        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        var service = await _commands.RunAsync("launchctl", "print system/dev.agent-up.server", cancellationToken);
        var cli = await _commands.RunAsync("bash", "-lc \"command -v agent-up\"", cancellationToken);

        return PostInstallValidation.Validate(new InstalledState(
            ServiceRegistered: service.ExitCode == 0,
            ServiceRunning: service.ExitCode == 0,
            CliAvailableFromFreshShell: cli.ExitCode == 0,
            DesktopInstalled: _files.FileExists(_options.Paths.DesktopInfoPlistPath),
            InstallerVersion: session.Version,
            CliVersion: session.Version,
            ServerVersion: session.Version,
            DesktopVersion: session.Version), session.Version);
    }

    private string BuildInstallScript(
        InstallerSession session,
        InstallSummary summary,
        Dictionary<string, string> tempFiles,
        MacOsInstallerManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("set -euo pipefail");

        if (summary.Includes(InstallerComponent.Desktop))
        {
            var appBundle = Q(_options.Paths.AppBundleDirectory);
            var macosDir = Q(Path.Join(_options.Paths.AppBundleDirectory, "Contents", "MacOS"));
            sb.AppendLine($"rm -rf {appBundle}");
            sb.AppendLine($"mkdir -p {macosDir}");
            sb.AppendLine($"mkdir -p {Q(_options.Paths.DesktopResourcesDirectory)}");
            sb.AppendLine($"cp -r {Q(_options.Payload.DesktopDirectory)}/. {macosDir}");
            sb.AppendLine($"cp {Q(_options.Payload.IconPath)} {Q(_options.Paths.DesktopIconPath)}");
            sb.AppendLine($"cp {Q(tempFiles[_options.Paths.DesktopInfoPlistPath])} {Q(_options.Paths.DesktopInfoPlistPath)}");
            sb.AppendLine($"chmod +x {Q(_options.Paths.DesktopExecutable)}");
            sb.AppendLine($"rm -f {Q(_options.Paths.DesktopSymlinkPath)}");
            sb.AppendLine($"ln -sf {Q(_options.Paths.DesktopExecutable)} {Q(_options.Paths.DesktopSymlinkPath)}");
        }

        if (summary.Includes(InstallerComponent.Server))
        {
            sb.AppendLine($"rm -rf {Q(_options.Paths.ServerDirectory)}");
            sb.AppendLine($"mkdir -p {Q(_options.Paths.ServerDirectory)}");
            sb.AppendLine($"cp -r {Q(_options.Payload.ServerDirectory)}/. {Q(_options.Paths.ServerDirectory)}");
            sb.AppendLine($"mkdir -p {Q(_options.Paths.ApplicationSupportDirectory)}");
            sb.AppendLine($"mkdir -p {Q(_options.Paths.LogsDirectory)}");
            sb.AppendLine($"chmod +x {Q(_options.Paths.ServerExecutable)}");
            sb.AppendLine($"cp {Q(tempFiles[_options.Paths.LaunchDaemonPath])} {Q(_options.Paths.LaunchDaemonPath)}");
            sb.AppendLine($"chown root:wheel {Q(_options.Paths.LaunchDaemonPath)}");
            sb.AppendLine($"chmod 644 {Q(_options.Paths.LaunchDaemonPath)}");
            sb.AppendLine($"launchctl bootstrap system {Q(_options.Paths.LaunchDaemonPath)}");
            sb.AppendLine($"launchctl kickstart -k system/{manifest.ServerLaunchDaemonLabel}");
            sb.AppendLine($"rm -f {Q(_options.Paths.ServerSymlinkPath)}");
            sb.AppendLine($"ln -sf {Q(_options.Paths.ServerExecutable)} {Q(_options.Paths.ServerSymlinkPath)}");
        }

        if (summary.Includes(InstallerComponent.Cli))
        {
            sb.AppendLine($"rm -rf {Q(_options.Paths.CliDirectory)}");
            sb.AppendLine($"mkdir -p {Q(_options.Paths.CliDirectory)}");
            sb.AppendLine($"cp -r {Q(_options.Payload.CliDirectory)}/. {Q(_options.Paths.CliDirectory)}");
            sb.AppendLine($"chmod +x {Q(_options.Paths.CliExecutable)}");
            sb.AppendLine($"rm -f {Q(_options.Paths.CliSymlinkPath)}");
            sb.AppendLine($"ln -sf {Q(_options.Paths.CliExecutable)} {Q(_options.Paths.CliSymlinkPath)}");
        }

        return sb.ToString();
    }

    private string BuildUninstallScript(InstallerComponentTarget target)
    {
        var sb = new StringBuilder();
        sb.AppendLine("set -euo pipefail");

        if (target == InstallerComponentTarget.Server)
        {
            sb.AppendLine($"launchctl bootout system {Q(_options.Paths.LaunchDaemonPath)} 2>/dev/null || true");
            sb.AppendLine($"rm -f {Q(_options.Paths.LaunchDaemonPath)}");
            sb.AppendLine($"rm -rf {Q(_options.Paths.ServerDirectory)}");
            sb.AppendLine($"rm -f {Q(_options.Paths.ServerSymlinkPath)}");
        }
        else if (target == InstallerComponentTarget.Cli)
        {
            sb.AppendLine($"rm -rf {Q(_options.Paths.CliDirectory)}");
            sb.AppendLine($"rm -f {Q(_options.Paths.CliSymlinkPath)}");
        }
        else if (target == InstallerComponentTarget.Desktop)
        {
            sb.AppendLine($"rm -rf {Q(_options.Paths.AppBundleDirectory)}");
            sb.AppendLine($"rm -f {Q(_options.Paths.DesktopSymlinkPath)}");
        }

        return sb.ToString();
    }

    private async Task RunElevatedAsync(string script, CancellationToken cancellationToken)
    {
        // Use /tmp directly — macOS PKG installer sets TMPDIR to a PKInstallSandbox path
        // not accessible to child processes spawned via Process.Start.
        var tmpFile = Path.Combine("/tmp", Path.GetRandomFileName());
        try
        {
            await File.WriteAllTextAsync(tmpFile, "#!/usr/bin/env bash\n" + script, cancellationToken);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmpFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            if (Environment.UserName == "root")
            {
                // Pass the path unquoted — Process.Start with UseShellExecute=false does not
                // process shell quotes in Arguments, so single-quoting is passed literally.
                await _requiredCommands.RunAsync("bash", tmpFile, cancellationToken);
            }
            else
            {
                // Write AppleScript to a file to avoid quoting issues in Arguments string.
                var appleScriptPath = Path.Combine("/tmp", Path.GetRandomFileName() + ".scpt");
                try
                {
                    await File.WriteAllTextAsync(appleScriptPath,
                        $"do shell script \"{tmpFile}\" with administrator privileges",
                        cancellationToken);
                    await _requiredCommands.RunAsync("osascript", appleScriptPath, cancellationToken);
                }
                finally
                {
                    try { File.Delete(appleScriptPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
        finally
        {
            try { File.Delete(tmpFile); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // Single-quote a path for shell use (escaping any embedded single quotes).
    private static string Q(string path) => $"'{path.Replace("'", "'\\''")}'";

    private static InstallerComponentTarget TargetFor(ProductComponent component)
        => Enum.TryParse<InstallerComponentTarget>(component.Id, true, out var t)
            ? t
            : throw new NotSupportedException($"Component '{component.Id}' is not supported by the macOS adapter.");
}
