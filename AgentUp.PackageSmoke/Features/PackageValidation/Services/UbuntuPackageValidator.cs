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

        var installerApp = SafeSmokePaths.Child(root, "opt", "agent-up", "installer", "AgentUp.InstallerApp");
        assert.ExecutableExists(installerApp, "ubuntu.installer");

        var payloadDesktop = SafeSmokePaths.Child(root, "opt", "agent-up", "installer", "payload", "desktop", "AgentUp.Desktop");
        var payloadServer = SafeSmokePaths.Child(root, "opt", "agent-up", "installer", "payload", "server", "AgentUp.Server");
        var payloadCli = SafeSmokePaths.Child(root, "opt", "agent-up", "installer", "payload", "cli", "AgentUp.CLI");
        assert.ExecutableExists(payloadDesktop, "ubuntu.desktop");
        assert.ExecutableExists(payloadServer, "ubuntu.server");
        assert.ExecutableExists(payloadCli, "ubuntu.cli");

        var serviceUnit = SafeSmokePaths.Child(root, "opt", "agent-up", "installer", "payload", "service", "agent-up-server.service");
        assert.FileExists(serviceUnit, "ubuntu.service");
        assert.Contains(serviceUnit, "ExecStart=/opt/agent-up/server/AgentUp.Server", "ubuntu.service.exec");
        assert.Contains(serviceUnit, "RestartSec=5", "ubuntu.service.restart");
        assert.Contains(serviceUnit, "DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/cache/agent-up", "ubuntu.service.bundle.extract");
        assert.Contains(serviceUnit, "CacheDirectory=agent-up", "ubuntu.service.cache");

        assert.FileExists(SafeSmokePaths.Child(root, "opt", "agent-up", "installer", "payload", "icon", "Agent-Up.png"), "ubuntu.payload.icon");
        assert.FileExists(SafeSmokePaths.Child(root, "usr", "share", "pixmaps", "agent-up.png"), "ubuntu.icon");

        var installerDesktop = SafeSmokePaths.Child(root, "usr", "share", "applications", "agent-up-installer.desktop");
        assert.FileExists(installerDesktop, "ubuntu.installer.desktop");

        var metainfo = SafeSmokePaths.Child(root, "usr", "share", "metainfo", "agent-up.metainfo.xml");
        assert.FileExists(metainfo, "ubuntu.metainfo");
        assert.Contains(metainfo, "<pkgname>agent-up</pkgname>", "ubuntu.metainfo.pkgname");
        assert.Contains(metainfo, "<release version=", "ubuntu.metainfo.version");

        var postinst = SafeSmokePaths.Child(control, "postinst");
        assert.Contains(postinst, "chmod +x /opt/agent-up/installer/AgentUp.InstallerApp", "ubuntu.postinst");
        assert.FileExists(SafeSmokePaths.Child(control, "prerm"), "ubuntu.prerm");

        return new PackageValidationResult(payloadServer, payloadCli, assert.Findings);
    }
}
