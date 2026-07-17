using AgentUp.Packaging.Features.MacOsPackages.Controllers;
using AgentUp.Packaging.Features.MacOsPackages.Providers;
using AgentUp.Packaging.Features.MacOsPackages.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;
using AgentUp.Packaging.Features.UbuntuPackages.Controllers;
using AgentUp.Packaging.Features.UbuntuPackages.Providers;
using AgentUp.Packaging.Features.UbuntuPackages.Services;
using AgentUp.Packaging.Features.WindowsPackages.Controllers;
using AgentUp.Packaging.Features.WindowsPackages.Providers;
using AgentUp.Packaging.Features.WindowsPackages.Services;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Shared.Providers;

namespace AgentUp.Packaging.Shared.Factories;

public sealed class PackagingServiceRegistry
{
    public PackageCommandController PackageCommands { get; }

    public PackagingServiceRegistry()
    {
        ICommandRunner commands = new ProcessCommandRunner();

        var ubuntuWriter = new FileSystemPackageWriter();
        var windowsWriter = new WindowsFileSystemPackageWriter();
        var macOsWriter = new MacOsFileSystemPackageWriter();

        var ubuntuStaging = new PayloadStagingController(new PackagePayloadStager(new PackagePublisher(commands), ubuntuWriter));
        var windowsStaging = new PayloadStagingController(new PackagePayloadStager(new PackagePublisher(commands), windowsWriter));
        var macOsStaging = new PayloadStagingController(new PackagePayloadStager(new PackagePublisher(commands), macOsWriter));

        var ubuntu = new UbuntuPackageController(new UbuntuPackager(ubuntuWriter, ubuntuStaging, new DpkgDebPackageTool(commands)));
        var windows = new WindowsPackageController(new WindowsPackager(windowsWriter, windowsStaging, new WindowsWixPackagingTool(commands)));
        var macOs = new MacOsPackageController(new MacOsPackager(macOsWriter, macOsStaging, new MacOsPackageTool(commands)));

        var environment = new EnvironmentVariableProvider();

        PackageCommands = new PackageCommandController(
            new PackageCommandParser(environment),
            new PackageCommandService(
            new RepositoryPathProvider(),
            environment,
            ubuntu,
            windows,
            macOs));
    }
}
