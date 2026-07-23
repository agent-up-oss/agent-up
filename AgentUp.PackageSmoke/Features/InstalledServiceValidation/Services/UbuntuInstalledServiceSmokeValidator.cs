using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;

public sealed class UbuntuInstalledServiceSmokeValidator : InstalledServiceSmokeValidator
{
    public UbuntuInstalledServiceSmokeValidator(ICommandRunner commands, IServerProbe serverProbe, IRuntimeSecurityChecks securityChecks)
        : base(commands, serverProbe, securityChecks)
    {
    }

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
        var installerPath = Path.Join("/opt", product.ArtifactBaseName, "installer", "AgentUp.InstallerApp");
        await RunRequiredAsync(assert, new CommandSpec("sudo", [installerPath, "--install-core"]), "installed.ubuntu.install-core", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("bash", ["-lc", $"command -v {product.CliShimName}"]), "installed.ubuntu.path", cancellationToken);
        assert.FileExists(Path.Join(request.SystemRoot, "usr", "share", "applications", $"{product.CliShimName}.desktop"), "installed.ubuntu.desktop.entry");
        assert.FileExists(Path.Join(request.SystemRoot, "usr", "share", "pixmaps", $"{product.CliShimName}.png"), "installed.ubuntu.icon");

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
}
