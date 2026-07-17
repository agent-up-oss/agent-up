using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

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
        var debPath = Path.Join(request.ArtifactDirectory, $"agent-up-ubuntu-{request.RuntimeId}.deb");
        assert.FileExists(debPath, "installed.ubuntu.artifact");
        if (!File.Exists(debPath))
            return null;

        await RunRequiredAsync(assert, new CommandSpec("sudo", ["apt-get", "install", "-y", debPath]), "installed.ubuntu.install", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("bash", ["-lc", "command -v agent-up"]), "installed.ubuntu.path", cancellationToken);
        assert.FileExists("/usr/share/applications/agent-up.desktop", "installed.ubuntu.desktop.entry");
        assert.FileExists("/usr/share/pixmaps/agent-up.png", "installed.ubuntu.icon");

        return new InstalledServiceContext(
            "agent-up",
            null,
            [new CommandSpec("sudo", ["apt-get", "purge", "-y", "agent-up"])],
            [
                new CommandSpec("sudo", ["systemctl", "status", "agent-up-server.service", "--no-pager"]),
                new CommandSpec("sudo", ["journalctl", "-u", "agent-up-server.service", "--no-pager", "-n", "200"]),
                new CommandSpec("sudo", ["tail", "-n", "200", "/var/log/agent-up-server.log"]),
                new CommandSpec("sudo", ["tail", "-n", "200", "/var/log/agent-up-server.err.log"]),
                new CommandSpec("ps", ["-ef"]),
                new CommandSpec("ss", ["-ltnp"]),
                new CommandSpec("sudo", ["ls", "-la", "/var/lib/agent-up"])
            ]);
    }
}
