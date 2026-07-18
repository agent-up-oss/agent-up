using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Services;

public sealed class MacOsPackageValidator : IPackageValidator
{
    private readonly IMacOsPackageArchiveProvider _archive;

    public MacOsPackageValidator(IMacOsPackageArchiveProvider archive)
    {
        _archive = archive;
    }

    public async Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default)
    {
        var assert = new FileAssertions();
        var archive = Path.Join(request.ArtifactDirectory, $"agent-up-macos-{request.RuntimeId}.pkg");
        var expanded = Path.Join(request.WorkDirectory, "pkg-expanded");
        assert.FileExists(archive, "macos.artifact");
        if (!File.Exists(archive))
            return new PackageValidationResult(null, null, assert.Findings);

        var expand = await _archive.ExpandAsync(archive, expanded, cancellationToken);
        if (!expand.Succeeded)
        {
            assert.Error("macos.expand", expand.ErrorMessage!);
            return new PackageValidationResult(null, null, assert.Findings);
        }

        var installerApp = _archive.FindFirst(expanded, Path.Join("Applications", "Agent-Up Installer.app", "Contents", "MacOS", "AgentUp.InstallerApp"));
        var installerPayloadDesktop = _archive.FindFirst(expanded, Path.Join("Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "desktop", "AgentUp.Desktop"));
        var installerPayloadServer = _archive.FindFirst(expanded, Path.Join("Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "server", "AgentUp.Server"));
        var installerPayloadCli = _archive.FindFirst(expanded, Path.Join("Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "cli", "AgentUp.CLI"));
        var distribution = _archive.FindDistribution(expanded);
        var postinstall = _archive.FindFirst(expanded, Path.Join("InstallerApp.pkg", "Scripts", "postinstall"));

        assert.ExecutableExists(installerApp, "macos.installer.app");
        assert.ExecutableExists(installerPayloadDesktop, "macos.installer.payload.desktop");
        assert.ExecutableExists(installerPayloadServer, "macos.installer.payload.server");
        assert.ExecutableExists(installerPayloadCli, "macos.installer.payload.cli");
        assert.Contains(distribution, "InstallerApp.pkg", "macos.distribution.installer");
        assert.Contains(postinstall, "open -a \"/Applications/Agent-Up Installer.app\"", "macos.postinstall.installer");

        return new PackageValidationResult(installerPayloadServer, installerPayloadCli, assert.Findings);
    }
}
