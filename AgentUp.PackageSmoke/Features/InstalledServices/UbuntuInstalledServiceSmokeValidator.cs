using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.InstalledServices;

public sealed class UbuntuInstalledServiceSmokeValidator : InstalledServiceSmokeValidator
{
    public UbuntuInstalledServiceSmokeValidator(ICommandRunner commands, IServerProbe serverProbe)
        : base(commands, serverProbe)
    {
    }

    protected override async Task<InstalledServiceContext?> InstallAsync(
        InstalledServiceSmokeRequest request,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var debPath = Path.Combine(request.ArtifactDirectory, $"agent-up-ubuntu-{request.RuntimeId}.deb");
        assert.FileExists(debPath, "installed.ubuntu.artifact");
        if (!File.Exists(debPath))
            return null;

        await RunRequiredAsync(assert, new CommandSpec("sudo", ["apt-get", "install", "-y", debPath]), "installed.ubuntu.install", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("bash", ["-lc", "command -v agent-up"]), "installed.ubuntu.path", cancellationToken);
        assert.FileExists("/usr/share/applications/agent-up.desktop", "installed.ubuntu.desktop.entry");
        assert.FileExists("/usr/share/pixmaps/agent-up.png", "installed.ubuntu.icon");

        return new InstalledServiceContext(
            "/usr/bin/agent-up",
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
