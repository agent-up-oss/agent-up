using System.Runtime.CompilerServices;
using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Interfaces;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.NixOsInstallation.Interfaces;
using LocalInstaller.Core.Features.PrerequisiteChecks.Models;

namespace LocalInstaller.Core.Features.NixOsInstallation.Providers;

public sealed class NixOsInstallerPlatformAdapter(
    INixOsExecutableLookup executables,
    DockerPrerequisite dockerPrerequisite) : IInstallerPlatformAdapter
{
    public string PlatformName => "NixOS";

    public bool SupportsInstallActions => false;

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await dockerPrerequisite.CheckAsync(cancellationToken);

    public Task<InstallerComponentStatus> GetComponentStatusAsync(
        ProductComponent component,
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var target = TargetFor(component);
        var executable = ExecutableName(session.Manifest, target);
        var path = executables.Find(executable);
        return Task.FromResult(path is null
            ? new InstallerComponentStatus(
                component,
                InstallerComponentStatusKind.NotInstalled,
                Message: $"{executable} was not found on PATH. Add {session.ProductName} through the NixOS or Home Manager module.")
            : new InstallerComponentStatus(
                component,
                InstallerComponentStatusKind.Installed,
                Message: $"Found {executable} at {path}. Managed by NixOS."));
    }

    public IReadOnlyList<InstallOperation> PlanComponentAction(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session)
        =>
        [
            new(
                InstallOperationKind.ValidateInstallation,
                $"{component.DisplayName} is managed by NixOS or Home Manager configuration",
                false)
        ];

    public async IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield return new InstallProgress(
            InstallOperationKind.ValidateInstallation,
            $"{component.DisplayName} install actions are disabled on NixOS. Change services.{session.Manifest.Slug} or programs.{session.Manifest.Slug} instead.",
            1,
            1);
    }

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        =>
        [
            new(
                InstallOperationKind.ValidateInstallation,
                $"{session.ProductName} is managed declaratively by NixOS or Home Manager",
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
            $"Install actions are disabled on NixOS. Change services.{session.Manifest.Slug} or programs.{session.Manifest.Slug} instead.",
            1,
            1);
    }

    public Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var findings = session.Manifest.InstallableComponents
            .Select(component =>
            {
                var target = TargetFor(component);
                var executable = ExecutableName(session.Manifest, target);
                var path = executables.Find(executable);
                var findingCode = $"{target}".ToLowerInvariant() + ".path";
                return path is null
                    ? new ValidationFinding(findingCode, $"{executable} was not found on PATH.", ValidationSeverity.Warning)
                    : new ValidationFinding(findingCode, $"{component.DisplayName} found at {path}.", ValidationSeverity.Info);
            })
            .ToList();

        return Task.FromResult(new ValidationReport(findings));
    }

    private static InstallerComponentTarget TargetFor(ProductComponent component)
        => component.Target
           ?? (Enum.TryParse<InstallerComponentTarget>(component.Id, true, out var t)
            ? t
            : throw new NotSupportedException($"Component '{component.Id}' is not supported by the NixOS adapter."));

    private static string ExecutableName(ProductManifest manifest, InstallerComponentTarget target)
        => target switch
        {
            InstallerComponentTarget.Desktop => $"{manifest.Slug}-desktop",
            InstallerComponentTarget.Server => $"{manifest.Slug}-server",
            InstallerComponentTarget.Cli => manifest.Slug,
            InstallerComponentTarget.Tray => $"{manifest.Slug}-tray",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
}
