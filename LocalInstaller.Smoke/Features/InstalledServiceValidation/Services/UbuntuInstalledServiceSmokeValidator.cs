using LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Interfaces;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Models;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Features.RuntimeSecurity.Interfaces;
using LocalInstaller.Smoke.Shared.Providers;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.Services;

public sealed class UbuntuInstalledServiceSmokeValidator : InstalledServiceSmokeValidator
{
    public UbuntuInstalledServiceSmokeValidator(ICommandRunner commands, IServerProbe serverProbe, IRuntimeSecurityChecks securityChecks)
        : base(commands, serverProbe, securityChecks) { }

    internal UbuntuInstalledServiceSmokeValidator(ICommandRunner commands, IServerProbe serverProbe, IRuntimeSecurityChecks securityChecks, HttpClient http)
        : base(commands, serverProbe, securityChecks, http) { }

    protected override async Task<InstalledServiceContext?> InstallAsync(
        InstalledServiceSmokeRequest request,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var product = request.Product;
        var debPath = Path.Join(request.ArtifactDirectory, $"{product.ArtifactBaseName}-ubuntu-{request.RuntimeId}.deb");
        assert.FileExists(debPath, "installed.ubuntu.artifact");
        if (!File.Exists(debPath))
            return null;

        await RunRequiredAsync(assert, new CommandSpec("sudo", ["apt-get", "install", "-y", debPath]), "installed.ubuntu.install", cancellationToken);
        var installCoreCommand = $"/opt/{product.ArtifactBaseName}/installer/{product.InstallerExecutableName} --install-core";
        await RunRequiredAsync(assert, new CommandSpec("sudo", ["bash", "-c", installCoreCommand]), "installed.ubuntu.install-core", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("bash", ["-lc", $"command -v {product.CliShimName}"]), "installed.ubuntu.path", cancellationToken);
        assert.FileExists(Path.Join(request.SystemRoot, "usr", "share", "applications", $"{product.CliShimName}.desktop"), "installed.ubuntu.desktop.entry");
        assert.FileExists(Path.Join(request.SystemRoot, "usr", "share", "pixmaps", $"{product.CliShimName}.png"), "installed.ubuntu.icon");
        assert.FileExists(Path.Join(request.SystemRoot, "opt", product.ArtifactBaseName, "tray", product.TrayExecutableName), "installed.ubuntu.tray");
        assert.FileExists(Path.Join(request.SystemRoot, "etc", "xdg", "autostart", $"{product.ArtifactBaseName}-tray.desktop"), "installed.ubuntu.tray.autostart");

        return new InstalledServiceContext(
            product.CliShimName,
            null,
            [new CommandSpec("sudo", ["apt-get", "purge", "-y", product.ArtifactBaseName])],
            [
                new CommandSpec("sudo", ["systemctl", "status", $"{product.ServiceName}.service", "--no-pager"]),
                new CommandSpec("sudo", ["journalctl", "-u", $"{product.ServiceName}.service", "--no-pager", "-n", "200"]),
                new CommandSpec("sudo", ["tail", "-n", "200", $"/var/log/{product.ServiceName}.log"]),
                new CommandSpec("sudo", ["tail", "-n", "200", $"/var/log/{product.ServiceName}.err.log"]),
                new CommandSpec("ps", ["-ef"]),
                new CommandSpec("ss", ["-ltnp"]),
                new CommandSpec("sudo", ["ls", "-la", $"/var/lib/{product.CliShimName}"])
            ]);
    }

    protected override async Task VerifyUninstalledAsync(
        InstalledServiceSmokeRequest request,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var product = request.Product;
        var service = await RunAsync(
            new CommandSpec("sudo", ["systemctl", "status", $"{product.ServiceName}.service", "--no-pager"]),
            cancellationToken);
        if (service.ExitCode == 0)
            assert.Error("installed.ubuntu.uninstall.service", $"{product.ServiceName}.service still exists after uninstall.");

        var cli = await RunAsync(new CommandSpec("bash", ["-lc", $"command -v {product.CliShimName}"]), cancellationToken);
        if (cli.ExitCode == 0)
            assert.Error("installed.ubuntu.uninstall.cli", $"{product.CliShimName} still exists on PATH after uninstall.");

        assert.FileDoesNotExist(Path.Join(request.SystemRoot, "usr", "share", "applications", $"{product.CliShimName}.desktop"),
            "installed.ubuntu.uninstall.desktop.entry");
        assert.FileDoesNotExist(Path.Join(request.SystemRoot, "etc", "xdg", "autostart", $"{product.ArtifactBaseName}-tray.desktop"),
            "installed.ubuntu.uninstall.tray.autostart");
    }
}
