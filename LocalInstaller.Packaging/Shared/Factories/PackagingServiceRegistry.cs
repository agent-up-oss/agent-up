using LocalInstaller.Packaging.Features.MacOsPackages.Controllers;
using LocalInstaller.Packaging.Features.MacOsPackages.Providers;
using LocalInstaller.Packaging.Features.MacOsPackages.Services;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Controllers;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Providers;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Services;
using LocalInstaller.Packaging.Features.UbuntuPackages.Controllers;
using LocalInstaller.Packaging.Features.UbuntuPackages.Providers;
using LocalInstaller.Packaging.Features.UbuntuPackages.Services;
using LocalInstaller.Packaging.Features.WindowsPackages.Controllers;
using LocalInstaller.Packaging.Features.WindowsPackages.Providers;
using LocalInstaller.Packaging.Features.WindowsPackages.Services;
using LocalInstaller.Packaging.Shared.Interfaces;
using LocalInstaller.Packaging.Shared.Providers;

namespace LocalInstaller.Packaging.Shared.Factories;

public sealed partial class PackagingServiceRegistry
{
    public PackageCommandController PackageCommands { get; }

    public PackagingServiceRegistry(string productName, string slug, string environmentPrefix, string? manufacturer = null, string? windowsUpgradeCode = null)
        : this(new PackageProductManifest(productName, slug, environmentPrefix)
        {
            Manufacturer = manufacturer,
            WindowsUpgradeCode = windowsUpgradeCode
        })
    {
        PackageProductManifest.Validate(new PackageProductManifest(productName, slug, environmentPrefix));
    }

    public PackagingServiceRegistry(PackageProductManifest product)
    {
        PackageProductManifest.Validate(product);
        ICommandRunner commands = new ProcessCommandRunner();

        var ubuntuWriter = new FileSystemPackageWriter();
        var windowsWriter = new WindowsFileSystemPackageWriter();
        var macOsWriter = new MacOsFileSystemPackageWriter();

        var ubuntuStaging = new PayloadStagingController(new PackagePayloadStager(new PackagePublisher(commands), ubuntuWriter));
        var windowsStaging = new PayloadStagingController(new PackagePayloadStager(new PackagePublisher(commands), windowsWriter));
        var macOsStaging = new PayloadStagingController(new PackagePayloadStager(new PackagePublisher(commands), macOsWriter));

        var ubuntu = new UbuntuPackageController(new UbuntuPackager(ubuntuWriter, ubuntuStaging, new DpkgDebPackageTool(commands), product));
        var windows = new WindowsPackageController(new WindowsPackager(windowsWriter, windowsStaging, new WindowsWixPackagingTool(commands)));
        var macOs = new MacOsPackageController(new MacOsPackager(macOsWriter, macOsStaging, new MacOsPackageTool(commands)));

        var environment = new EnvironmentVariableProvider();
        var parser = new PackageCommandParser(environment);

        PackageCommands = new PackageCommandController(
            new PackageCommandService(
                parser,
                new RepositoryPathProvider(),
                environment,
                ubuntu,
                windows,
                macOs,
                product));
    }
}
