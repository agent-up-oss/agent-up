using System.Diagnostics;
using System.Text;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.UbuntuInstallation.DTOs;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Models;

namespace AgentUp.Installers.Features.UbuntuInstallation.Providers;

public sealed class UbuntuInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    private readonly ICommandRunner _commands;
    private readonly IUbuntuInstallerFileSystem _files;
    private readonly UbuntuInstallerOptions _options;
    private readonly IRequiredCommandRunner _requiredCommands;
    private readonly DockerPrerequisite _dockerPrerequisite;

    public UbuntuInstallerPlatformAdapter(
        ICommandRunner commands,
        IUbuntuInstallerFileSystem files,
        UbuntuInstallerOptions options,
        IRequiredCommandRunner requiredCommands,
        DockerPrerequisite dockerPrerequisite)
    {
        _commands = commands;
        _files = files;
        _options = options;
        _requiredCommands = requiredCommands;
        _dockerPrerequisite = dockerPrerequisite;
    }

    public string PlatformName => "Ubuntu";

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
        var manifest = _options.Manifest;
        var paths = _options.Paths;
        var operations = new List<InstallOperation>
        {
            new(InstallOperationKind.ValidatePrerequisites, "Validate Docker and Ubuntu prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Use {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, $"Install selected {manifest.DesktopApplicationName} files under {paths.RootDirectory}", true)
        };

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterService, $"Register and start {manifest.ServiceUnitName}", true));
        if (summary.Includes(InstallerComponent.Cli))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterCli, $"Register {paths.CliSymlinkPath}", true));
        if (summary.Includes(InstallerComponent.Desktop))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterDesktop, $"Register {manifest.DesktopApplicationName} desktop launcher", true));

        operations.Add(new InstallOperation(InstallOperationKind.RegisterUninstall, "Register Ubuntu package-manager uninstall metadata", true));
        operations.Add(new InstallOperation(InstallOperationKind.ValidateInstallation, "Validate Ubuntu installed state", false));
        return operations;
    }

    public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = PlanInstall(session);
        var progress = new InstallProgressTracker(operations);
        var summary = session.Summary();

        yield return progress.Complete(InstallOperationKind.ValidatePrerequisites);
        yield return progress.Complete(InstallOperationKind.StagePayload);

        var tempFiles = new Dictionary<string, string>();
        try
        {
            if (summary.Includes(InstallerComponent.Desktop))
            {
                var tmpEntry = Path.Join("/tmp", Path.GetRandomFileName());
                await File.WriteAllTextAsync(tmpEntry,
                    _options.Manifest.DesktopEntryText(_options.Paths.DesktopExecutable, session.Version.ToString()),
                    cancellationToken);
                tempFiles[_options.Paths.DesktopEntryPath] = tmpEntry;
            }

            await RunElevatedAsync(BuildInstallScript(summary, tempFiles), cancellationToken);
        }
        finally
        {
            foreach (var tmp in tempFiles.Values)
            {
                try { File.Delete(tmp); }
                catch (IOException ex) { Trace.TraceWarning(ex.Message); }
                catch (UnauthorizedAccessException ex) { Trace.TraceWarning(ex.Message); }
            }
        }

        yield return progress.Complete(InstallOperationKind.InstallFiles);

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
            yield return progress.Complete(InstallOperationKind.RegisterService);
        if (summary.Includes(InstallerComponent.Cli))
            yield return progress.Complete(InstallOperationKind.RegisterCli);
        if (summary.Includes(InstallerComponent.Desktop))
            yield return progress.Complete(InstallOperationKind.RegisterDesktop);

        await _commands.RunAsync("dpkg-query", ["-W", _options.Manifest.PackageName], cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterUninstall);
        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    private async IAsyncEnumerable<InstallProgress> ExecuteUninstallAsync(
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

    private string BuildInstallScript(InstallSummary summary, Dictionary<string, string> tempFiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine($"mkdir -p {Q(_options.Paths.DataDirectory)}");
        sb.AppendLine($"touch {Q(_options.Paths.LogPath)}");
        sb.AppendLine($"touch {Q(_options.Paths.ErrorLogPath)}");

        if (summary.Includes(InstallerComponent.Desktop))
        {
            sb.AppendLine($"rm -rf {Q(_options.Paths.DesktopDirectory)}");
            sb.AppendLine($"mkdir -p {Q(_options.Paths.DesktopDirectory)}");
            sb.AppendLine($"cp -r {Q(_options.Payload.DesktopDirectory)}/. {Q(_options.Paths.DesktopDirectory)}");
            sb.AppendLine($"cp {Q(_options.Payload.IconPath)} {Q(_options.Paths.IconPath)}");
            sb.AppendLine($"chmod +x {Q(_options.Paths.DesktopExecutable)}");
            sb.AppendLine($"cp {Q(tempFiles[_options.Paths.DesktopEntryPath])} {Q(_options.Paths.DesktopEntryPath)}");
            sb.AppendLine("update-desktop-database /usr/share/applications 2>/dev/null || true");
        }

        if (summary.Includes(InstallerComponent.Server))
        {
            sb.AppendLine($"rm -rf {Q(_options.Paths.ServerDirectory)}");
            sb.AppendLine($"mkdir -p {Q(_options.Paths.ServerDirectory)}");
            sb.AppendLine($"cp -r {Q(_options.Payload.ServerDirectory)}/. {Q(_options.Paths.ServerDirectory)}");
            sb.AppendLine($"chmod +x {Q(_options.Paths.ServerExecutable)}");
            sb.AppendLine($"cp {Q(_options.Payload.ServiceFilePath)} {Q(_options.Paths.ServicePath)}");
            sb.AppendLine("systemctl daemon-reload");
            sb.AppendLine($"systemctl enable --now {Q(_options.Manifest.ServiceUnitName)}");
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

        switch (target)
        {
            case InstallerComponentTarget.Server:
                sb.AppendLine($"systemctl disable --now {Q(_options.Manifest.ServiceUnitName)} 2>/dev/null || true");
                sb.AppendLine($"rm -f {Q(_options.Paths.ServicePath)}");
                sb.AppendLine("systemctl daemon-reload");
                sb.AppendLine($"rm -rf {Q(_options.Paths.ServerDirectory)}");
                break;
            case InstallerComponentTarget.Cli:
                sb.AppendLine($"rm -f {Q(_options.Paths.CliSymlinkPath)}");
                sb.AppendLine($"rm -rf {Q(_options.Paths.CliDirectory)}");
                break;
            case InstallerComponentTarget.Desktop:
                sb.AppendLine($"rm -f {Q(_options.Paths.DesktopEntryPath)}");
                sb.AppendLine("update-desktop-database /usr/share/applications 2>/dev/null || true");
                sb.AppendLine($"rm -f {Q(_options.Paths.IconPath)}");
                sb.AppendLine($"rm -rf {Q(_options.Paths.DesktopDirectory)}");
                break;
        }

        return sb.ToString();
    }

    private async Task RunElevatedAsync(string script, CancellationToken cancellationToken)
    {
        // Use /tmp directly — same rationale as macOS: avoid sandbox-constrained TMPDIR.
        var tmpFile = Path.Join("/tmp", Path.GetRandomFileName());
        try
        {
            await File.WriteAllTextAsync(tmpFile, "#!/usr/bin/env bash\n" + script, cancellationToken);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmpFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            if (Environment.IsPrivilegedProcess)
                await _requiredCommands.RunAsync("bash", [tmpFile], cancellationToken);
            else
                await _requiredCommands.RunAsync("pkexec", ["bash", tmpFile], cancellationToken);
        }
        finally
        {
            try { File.Delete(tmpFile); }
            catch (IOException ex) { Trace.TraceWarning(ex.Message); }
            catch (UnauthorizedAccessException ex) { Trace.TraceWarning(ex.Message); }
        }
    }

    private static string Q(string path) => $"'{path.Replace("'", "'\\''")}'";

    private static InstallerComponentTarget TargetFor(ProductComponent component)
        => Enum.TryParse<InstallerComponentTarget>(component.Id, true, out var t)
            ? t
            : throw new NotSupportedException($"Component '{component.Id}' is not supported by the Ubuntu adapter.");

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        var service = await _commands.RunAsync("systemctl", ["is-enabled", _options.Manifest.ServiceUnitName], cancellationToken);
        var running = await _commands.RunAsync("systemctl", ["is-active", _options.Manifest.ServiceUnitName], cancellationToken);
        var cli = await _commands.RunAsync("bash", ["-lc", "command -v \"$1\"", "--", _options.Manifest.CliCommandName], cancellationToken);

        return PostInstallValidation.Validate(new InstalledState(
            ServiceRegistered: service.ExitCode == 0,
            ServiceRunning: running.ExitCode == 0,
            CliAvailableFromFreshShell: cli.ExitCode == 0,
            DesktopInstalled: _files.FileExists(_options.Paths.DesktopEntryPath),
            InstallerVersion: session.Version,
            CliVersion: session.Version,
            ServerVersion: session.Version,
            DesktopVersion: session.Version), session.Version);
    }
}
