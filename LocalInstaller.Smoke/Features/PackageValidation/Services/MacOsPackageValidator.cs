using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Shared.Providers;

namespace LocalInstaller.Smoke.Features.PackageValidation.Services;

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
        var product = request.Product;
        var pkg = product.ArtifactBaseName;
        var appBundleName = $"{product.DisplayName} Installer.app";
        var iconFileName = $"{product.DisplayName.Replace(" ", "-")}.png";
        var archive = SafeSmokePaths.Child(request.ArtifactDirectory, $"{pkg}-macos-{request.RuntimeId}.pkg");
        var expanded = SafeSmokePaths.Child(request.WorkDirectory, "pkg-expanded");
        assert.FileExists(archive, "macos.artifact");
        if (!File.Exists(archive))
            return new PackageValidationResult(null, null, assert.Findings);

        var expand = await _archive.ExpandAsync(archive, expanded, cancellationToken);
        if (!expand.Succeeded)
        {
            assert.Error("macos.expand", expand.ErrorMessage!);
            return new PackageValidationResult(null, null, assert.Findings);
        }

        var appBase = Path.Join("Applications", appBundleName);
        var installerApp = _archive.FindFirst(expanded, Path.Join(appBase, "Contents", "MacOS", product.InstallerExecutableName));
        var installerInfoPlist = _archive.FindFirst(expanded, Path.Join(appBase, "Contents", "Info.plist"));
        var installerIcon = _archive.FindFirst(expanded, Path.Join(appBase, "Contents", "Resources", iconFileName));
        var installerPayloadDesktop = _archive.FindFirst(expanded, Path.Join(appBase, "Contents", "MacOS", "payload", "desktop", product.DesktopExecutableName));
        var installerPayloadServer = _archive.FindFirst(expanded, Path.Join(appBase, "Contents", "MacOS", "payload", "server", product.ServerExecutableName));
        var installerPayloadCli = _archive.FindFirst(expanded, Path.Join(appBase, "Contents", "MacOS", "payload", "cli", product.CliExecutableName));
        var installerPayloadIcon = _archive.FindFirst(expanded, Path.Join(appBase, "Contents", "MacOS", "payload", "icon", iconFileName));
        var distribution = _archive.FindDistribution(expanded);
        var postinstall = _archive.FindFirst(expanded, Path.Join("InstallerApp.pkg", "Scripts", "postinstall"));

        assert.ExecutableExists(installerApp, "macos.installer.app");
        assert.FileExists(installerIcon, "macos.installer.icon");
        assert.FileExists(installerPayloadIcon, "macos.installer.payload.icon");
        assert.ExecutableExists(installerPayloadDesktop, "macos.installer.payload.desktop");
        assert.ExecutableExists(installerPayloadServer, "macos.installer.payload.server");
        assert.ExecutableExists(installerPayloadCli, "macos.installer.payload.cli");
        assert.Contains(installerInfoPlist, "CFBundleIconFile", "macos.installer.info.icon.key");
        assert.Contains(installerInfoPlist, iconFileName, "macos.installer.info.icon.file");
        assert.Contains(distribution, "InstallerApp.pkg", "macos.distribution.installer");
        assert.Contains(postinstall, $"open -a \"/Applications/{appBundleName}\"", "macos.postinstall.installer");

        return new PackageValidationResult(installerPayloadServer, installerPayloadCli, assert.Findings);
    }
}
