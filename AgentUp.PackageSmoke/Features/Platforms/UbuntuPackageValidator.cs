using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.Platforms;

public sealed class UbuntuPackageValidator : IPackageValidator
{
    private readonly ICommandRunner _commands;

    public UbuntuPackageValidator(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default)
    {
        var assert = new FileAssertions();
        var archive = Path.Combine(request.ArtifactDirectory, $"agent-up-ubuntu-{request.RuntimeId}.deb");
        var root = Path.Combine(request.WorkDirectory, "root");
        var control = Path.Combine(request.WorkDirectory, "control");
        assert.FileExists(archive, "ubuntu.artifact");

        if (!File.Exists(archive))
            return new PackageValidationResult(null, null, assert.Findings);

        Directory.CreateDirectory(request.WorkDirectory);
        await RunRequiredAsync(assert, new CommandSpec("dpkg-deb", ["-x", archive, root]), "ubuntu.extract", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("dpkg-deb", ["-e", archive, control]), "ubuntu.control", cancellationToken);

        var desktop = Path.Combine(root, "opt", "agent-up", "desktop", "AgentUp.Desktop");
        var server = Path.Combine(root, "opt", "agent-up", "server", "AgentUp.Server");
        var cli = Path.Combine(root, "opt", "agent-up", "cli", "AgentUp.CLI");
        assert.ExecutableExists(desktop, "ubuntu.desktop");
        assert.ExecutableExists(server, "ubuntu.server");
        assert.ExecutableExists(cli, "ubuntu.cli");
        assert.SymlinkExists(Path.Combine(root, "usr", "bin", "agent-up"), "ubuntu.cli.path");
        assert.FileExists(Path.Combine(root, "usr", "share", "applications", "agent-up.desktop"), "ubuntu.desktop.entry");
        assert.FileExists(Path.Combine(root, "usr", "share", "pixmaps", "agent-up.png"), "ubuntu.icon");
        assert.Contains(Path.Combine(root, "etc", "systemd", "system", "agent-up-server.service"), "ExecStart=/opt/agent-up/server/AgentUp.Server", "ubuntu.service.exec");
        assert.Contains(Path.Combine(root, "etc", "systemd", "system", "agent-up-server.service"), "RestartSec=5", "ubuntu.service.restart");
        assert.Contains(Path.Combine(root, "usr", "share", "applications", "agent-up.desktop"), "Exec=/opt/agent-up/desktop/AgentUp.Desktop", "ubuntu.desktop.exec");
        assert.Contains(Path.Combine(root, "usr", "share", "applications", "agent-up.desktop"), "Icon=agent-up", "ubuntu.desktop.icon");
        assert.Contains(Path.Combine(control, "postinst"), "systemctl enable --now agent-up-server.service", "ubuntu.postinst");
        assert.FileExists(Path.Combine(control, "prerm"), "ubuntu.prerm");

        return new PackageValidationResult(server, cli, assert.Findings);
    }

    private async Task RunRequiredAsync(FileAssertions assert, CommandSpec command, string code, CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
            assert.Error(code, $"{command.FileName} failed: {result.Stderr}{result.Stdout}");
    }
}
