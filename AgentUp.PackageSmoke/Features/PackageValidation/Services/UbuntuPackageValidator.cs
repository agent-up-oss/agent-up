using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Services;

public sealed class UbuntuPackageValidator : IPackageValidator
{
    private readonly IUbuntuPackageArchiveProvider _archive;

    public UbuntuPackageValidator(IUbuntuPackageArchiveProvider archive)
    {
        _archive = archive;
    }

    public async Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default)
    {
        var assert = new FileAssertions();
        var archive = Path.Join(request.ArtifactDirectory, $"agent-up-ubuntu-{request.RuntimeId}.deb");
        var root = Path.Join(request.WorkDirectory, "root");
        var control = Path.Join(request.WorkDirectory, "control");
        assert.FileExists(archive, "ubuntu.artifact");

        if (!File.Exists(archive))
            return new PackageValidationResult(null, null, assert.Findings);

        Directory.CreateDirectory(request.WorkDirectory);
        var rootExtract = await _archive.ExtractRootAsync(archive, root, cancellationToken);
        if (!rootExtract.Succeeded)
            assert.Error("ubuntu.extract", rootExtract.ErrorMessage!);

        var controlExtract = await _archive.ExtractControlAsync(archive, control, cancellationToken);
        if (!controlExtract.Succeeded)
            assert.Error("ubuntu.control", controlExtract.ErrorMessage!);

        var desktop = Path.Join(root, "opt", "agent-up", "desktop", "AgentUp.Desktop");
        var server = Path.Join(root, "opt", "agent-up", "server", "AgentUp.Server");
        var cli = Path.Join(root, "opt", "agent-up", "cli", "AgentUp.CLI");
        assert.ExecutableExists(desktop, "ubuntu.desktop");
        assert.ExecutableExists(server, "ubuntu.server");
        assert.ExecutableExists(cli, "ubuntu.cli");
        assert.SymlinkExists(Path.Join(root, "usr", "bin", "agent-up"), "ubuntu.cli.path");
        assert.FileExists(Path.Join(root, "usr", "share", "applications", "agent-up.desktop"), "ubuntu.desktop.entry");
        assert.FileExists(Path.Join(root, "usr", "share", "pixmaps", "agent-up.png"), "ubuntu.icon");
        assert.Contains(Path.Join(root, "etc", "systemd", "system", "agent-up-server.service"), "ExecStart=/opt/agent-up/server/AgentUp.Server", "ubuntu.service.exec");
        assert.Contains(Path.Join(root, "etc", "systemd", "system", "agent-up-server.service"), "RestartSec=5", "ubuntu.service.restart");
        assert.Contains(Path.Join(root, "etc", "systemd", "system", "agent-up-server.service"), "DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/cache/agent-up", "ubuntu.service.bundle.extract");
        assert.Contains(Path.Join(root, "etc", "systemd", "system", "agent-up-server.service"), "CacheDirectory=agent-up", "ubuntu.service.cache");
        assert.Contains(Path.Join(root, "usr", "share", "applications", "agent-up.desktop"), "Exec=/opt/agent-up/desktop/AgentUp.Desktop", "ubuntu.desktop.exec");
        assert.Contains(Path.Join(root, "usr", "share", "applications", "agent-up.desktop"), "Icon=agent-up", "ubuntu.desktop.icon");
        assert.Contains(Path.Join(control, "postinst"), "systemctl enable --now agent-up-server.service", "ubuntu.postinst");
        assert.FileExists(Path.Join(control, "prerm"), "ubuntu.prerm");

        return new PackageValidationResult(server, cli, assert.Findings);
    }
}
