using System.Runtime.CompilerServices;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.NixOsInstallation.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Features.NixOsInstallation.Providers;

public sealed class NixOsInstallerPlatformAdapter(
    INixOsExecutableLookup executables,
    DockerPrerequisite dockerPrerequisite) : IInstallerPlatformAdapter
{
    public string PlatformName => "NixOS";

    public bool SupportsInstallActions => false;

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await dockerPrerequisite.CheckAsync(cancellationToken);

    public Task<InstallerComponentStatus> GetComponentStatusAsync(
        InstallerComponentTarget target,
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var executable = ExecutableName(target);
        var path = executables.Find(executable);
        return Task.FromResult(path is null
            ? new InstallerComponentStatus(
                target,
                InstallerComponentStatusKind.NotInstalled,
                Message: $"{executable} was not found on PATH. Add Agent-Up through the NixOS or Home Manager module.")
            : new InstallerComponentStatus(
                target,
                InstallerComponentStatusKind.Installed,
                Message: $"Found {executable} at {path}. Managed by NixOS."));
    }

    public IReadOnlyList<InstallOperation> PlanComponentAction(
        InstallerComponentTarget target,
        InstallerComponentAction action,
        InstallerSession session)
        =>
        [
            new(
                InstallOperationKind.ValidateInstallation,
                $"{DisplayName(target)} is managed by NixOS or Home Manager configuration",
                false)
        ];

    public async IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
        InstallerComponentTarget target,
        InstallerComponentAction action,
        InstallerSession session,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield return new InstallProgress(
            InstallOperationKind.ValidateInstallation,
            $"{DisplayName(target)} install actions are disabled on NixOS. Change services.agent-up or programs.agent-up instead.",
            1,
            1);
    }

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        =>
        [
            new(
                InstallOperationKind.ValidateInstallation,
                "Agent-Up is managed declaratively by NixOS or Home Manager",
                false)
        ];

    public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield return new InstallProgress(
            InstallOperationKind.ValidateInstallation,
            "Install actions are disabled on NixOS. Change services.agent-up or programs.agent-up instead.",
            1,
            1);
    }

    public Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var findings = Enum.GetValues<InstallerComponentTarget>()
            .Select(target =>
            {
                var executable = ExecutableName(target);
                var path = executables.Find(executable);
                return path is null
                    ? new ValidationFinding($"{target.ToString().ToLowerInvariant()}.path", $"{executable} was not found on PATH.", ValidationSeverity.Warning)
                    : new ValidationFinding($"{target.ToString().ToLowerInvariant()}.path", $"{DisplayName(target)} found at {path}.", ValidationSeverity.Info);
            })
            .ToList();

        return Task.FromResult(new ValidationReport(findings));
    }

    private static string ExecutableName(InstallerComponentTarget target)
        => target switch
        {
            InstallerComponentTarget.Desktop => "agent-up-desktop",
            InstallerComponentTarget.Server => "agent-up-server",
            InstallerComponentTarget.Cli => "agent-up",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };

    private static string DisplayName(InstallerComponentTarget target)
        => target switch
        {
            InstallerComponentTarget.Desktop => "Desktop",
            InstallerComponentTarget.Server => "Server",
            InstallerComponentTarget.Cli => "CLI",
            _ => target.ToString()
        };
}
