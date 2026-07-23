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
        var archive = SafeSmokePaths.Child(request.ArtifactDirectory, $"agent-up-ubuntu-{request.RuntimeId}.deb");
        var root = SafeSmokePaths.Child(request.WorkDirectory, "root");
        var control = SafeSmokePaths.Child(request.WorkDirectory, "control");
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

        var desktop = SafeSmokePaths.Child(root, "opt", "agent-up", "desktop", "AgentUp.Desktop");
        var server = SafeSmokePaths.Child(root, "opt", "agent-up", "server", "AgentUp.Server");
        var cli = SafeSmokePaths.Child(root, "opt", "agent-up", "cli", "AgentUp.CLI");
        assert.ExecutableExists(desktop, "ubuntu.desktop");
        assert.ExecutableExists(server, "ubuntu.server");
        assert.ExecutableExists(cli, "ubuntu.cli");
        var desktopEntry = SafeSmokePaths.Child(root, "usr", "share", "applications", "agent-up.desktop");
        var serviceUnit = SafeSmokePaths.Child(root, "etc", "systemd", "system", "agent-up-server.service");
        assert.SymlinkExists(SafeSmokePaths.Child(root, "usr", "bin", "agent-up"), "ubuntu.cli.path");
        assert.FileExists(desktopEntry, "ubuntu.desktop.entry");
        assert.FileExists(SafeSmokePaths.Child(root, "usr", "share", "pixmaps", "agent-up.png"), "ubuntu.icon");
        assert.Contains(serviceUnit, "ExecStart=/opt/agent-up/server/AgentUp.Server", "ubuntu.service.exec");
        assert.Contains(serviceUnit, "RestartSec=5", "ubuntu.service.restart");
        assert.Contains(serviceUnit, "DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/cache/agent-up", "ubuntu.service.bundle.extract");
        assert.Contains(serviceUnit, "CacheDirectory=agent-up", "ubuntu.service.cache");
        assert.Contains(desktopEntry, "Exec=/opt/agent-up/desktop/AgentUp.Desktop", "ubuntu.desktop.exec");
        assert.Contains(desktopEntry, "Icon=agent-up", "ubuntu.desktop.icon");
        assert.Contains(SafeSmokePaths.Child(control, "postinst"), "systemctl enable --now agent-up-server.service", "ubuntu.postinst");
        assert.FileExists(SafeSmokePaths.Child(control, "prerm"), "ubuntu.prerm");

        return new PackageValidationResult(server, cli, assert.Findings);
    }
}
