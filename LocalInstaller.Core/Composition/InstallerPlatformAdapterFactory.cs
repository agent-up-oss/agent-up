using LocalInstaller.Core.Features.Installation.Interfaces;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.Installation.Providers;
using LocalInstaller.Core.Features.MacOsInstallation.DTOs;
using LocalInstaller.Core.Features.MacOsInstallation.Providers;
using LocalInstaller.Core.Features.NixOsInstallation.Providers;
using LocalInstaller.Core.Features.PrerequisiteChecks.Interfaces;
using LocalInstaller.Core.Features.PrerequisiteChecks.Providers;
using LocalInstaller.Core.Features.PrerequisiteChecks.Models;
using LocalInstaller.Core.Features.UbuntuInstallation.DTOs;
using LocalInstaller.Core.Features.UbuntuInstallation.Models;
using LocalInstaller.Core.Features.UbuntuInstallation.Providers;
using LocalInstaller.Core.Features.WindowsInstallation.DTOs;
using LocalInstaller.Core.Features.WindowsInstallation.Models;
using LocalInstaller.Core.Features.WindowsInstallation.Providers;

namespace LocalInstaller.Core.Composition;

public static partial class InstallerPlatformAdapterFactory
{
    public static IInstallerPlatformAdapter Create(
        ProductManifest manifest,
        string appBaseDirectory,
        string? fakeInstaller,
        bool useNixOsLookupOnlyMode)
    {
        if (fakeInstaller == "1")
            return new FakeInstallerPlatformAdapter(CurrentPlatformName(useNixOsLookupOnlyMode) + " dry run");

        if (OperatingSystem.IsLinux())
        {
            if (useNixOsLookupOnlyMode)
                return CreateNixOsAdapter();

            var payloadRoot = ResolvePayloadRoot(appBaseDirectory, manifest);
            return CreateUbuntuAdapter(payloadRoot, manifest);
        }
        if (OperatingSystem.IsMacOS())
        {
            var payloadRoot = ResolvePayloadRoot(appBaseDirectory, manifest);
            return CreateMacOsAdapter(payloadRoot, manifest);
        }
        if (OperatingSystem.IsWindows())
        {
            var payloadRoot = ResolvePayloadRoot(appBaseDirectory, manifest);
            return CreateWindowsAdapter(payloadRoot, manifest);
        }

        throw new PlatformNotSupportedException($"{manifest.ProductName} installer does not support this operating system.");
    }

    public static IInstallerPlatformAdapter CreateFake(string platformName)
        => new FakeInstallerPlatformAdapter(platformName);

    public static string ResolvePayloadRoot(string appBaseDirectory, ProductManifest manifest)
    {
        var payloadRoot = Environment.GetEnvironmentVariable(manifest.PayloadRootVariable);
        if (!string.IsNullOrWhiteSpace(payloadRoot))
            return payloadRoot;

        foreach (var candidateDirectory in PayloadCandidateDirectories(appBaseDirectory))
        {
            var bundledPayloadRoot = System.IO.Path.Join(candidateDirectory, "payload");
            if (IsPayloadRoot(bundledPayloadRoot, manifest))
                return bundledPayloadRoot;
        }

        throw new InvalidOperationException($"{manifest.PayloadRootVariable} must point at a payload root containing the registered installer option directories, or the installer app must include a bundled payload directory next to the executable.");
    }

    public static IReadOnlyList<string> PayloadCandidateDirectories(string appBaseDirectory)
    {
        var candidates = new List<string>();
        AddCandidate(candidates, appBaseDirectory);

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
            AddCandidate(candidates, System.IO.Path.GetDirectoryName(processPath));

        return candidates;
    }

    private static bool IsPayloadRoot(string payloadRoot, ProductManifest manifest)
    {
        if (manifest.InstallableComponents.Count > 0)
            return manifest.InstallableComponents.All(component => Directory.Exists(System.IO.Path.Join(payloadRoot, PayloadDirectoryName(component))));

        return Directory.Exists(System.IO.Path.Join(payloadRoot, "desktop")) &&
               Directory.Exists(System.IO.Path.Join(payloadRoot, "server")) &&
               Directory.Exists(System.IO.Path.Join(payloadRoot, "cli")) &&
               Directory.Exists(System.IO.Path.Join(payloadRoot, "tray"));
    }

    private static void AddCandidate(List<string> candidates, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        var fullPath = System.IO.Path.GetFullPath(candidate);
        if (!candidates.Contains(fullPath, StringComparer.Ordinal))
            candidates.Add(fullPath);
    }

    private static IInstallerPlatformAdapter CreateUbuntuAdapter(string payloadRoot, ProductManifest product)
    {
        var composition = Composition();
        var manifest = UbuntuInstallerManifest.ForProduct(product);
        var paths = UbuntuInstallerPaths.ForProduct(manifest);
        var payload = new UbuntuInstallPayload(
            DesktopDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Desktop, payloadRoot, "desktop"),
            ServerDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Server, payloadRoot, "server"),
            CliDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Cli, payloadRoot, "cli"),
            TrayDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Tray, payloadRoot, "tray"),
            ServiceFilePath: System.IO.Path.Join(payloadRoot, "service", manifest.ServiceUnitName),
            IconPath: System.IO.Path.Join(payloadRoot, "icon", product.ProductName + ".png"));

        return new UbuntuInstallerPlatformAdapter(
            composition.Commands,
            new UbuntuInstallerFileSystem(),
            new UbuntuInstallerOptions(payload, paths, manifest),
            composition.RequiredCommands,
            composition.DockerPrerequisite);
    }

    private static IInstallerPlatformAdapter CreateNixOsAdapter()
    {
        var composition = Composition();
        return new NixOsInstallerPlatformAdapter(new NixOsPathExecutableLookup(), composition.DockerPrerequisite);
    }

    private static IInstallerPlatformAdapter CreateMacOsAdapter(string payloadRoot, ProductManifest product)
    {
        var composition = Composition();
        var payload = new MacOsInstallPayload(
            DesktopDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Desktop, payloadRoot, "desktop"),
            ServerDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Server, payloadRoot, "server"),
            CliDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Cli, payloadRoot, "cli"),
            TrayDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Tray, payloadRoot, "tray"),
            IconPath: System.IO.Path.Join(payloadRoot, "icon", product.ProductName + ".png"));

        return new MacOsInstallerPlatformAdapter(
            composition.Commands,
            new MacOsInstallerFileSystem(),
            new MacOsInstallerOptions(payload),
            composition.RequiredCommands,
            composition.DockerPrerequisite);
    }

    private static IInstallerPlatformAdapter CreateWindowsAdapter(string payloadRoot, ProductManifest product)
    {
        var composition = Composition();
        var payload = new WindowsInstallPayload(
            DesktopDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Desktop, payloadRoot, "desktop"),
            ServerDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Server, payloadRoot, "server"),
            CliDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Cli, payloadRoot, "cli"),
            TrayDirectory: PayloadDirectoryFor(product, InstallerComponentTarget.Tray, payloadRoot, "tray"));

        return new WindowsInstallerPlatformAdapter(
            composition.Commands,
            new WindowsInstallerFileSystem(),
            new WindowsInstallerOptions(payload, WindowsInstallerPaths.ForProduct(product)),
            composition.RequiredCommands,
            composition.DockerPrerequisite);
    }

    private static string CurrentPlatformName(bool useNixOsLookupOnlyMode)
    {
        if (OperatingSystem.IsLinux() && useNixOsLookupOnlyMode)
            return "NixOS";
        if (OperatingSystem.IsWindows())
            return "Windows";
        if (OperatingSystem.IsMacOS())
            return "macOS";
        return "Linux";
    }

    private static string PayloadDirectoryFor(
        ProductManifest product,
        InstallerComponentTarget target,
        string payloadRoot,
        string legacyDirectoryName)
    {
        var component = product.InstallableComponents.FirstOrDefault(c => ComponentTarget(c) == target);
        return System.IO.Path.Join(payloadRoot, component is null ? legacyDirectoryName : PayloadDirectoryName(component));
    }

    private static string PayloadDirectoryName(ProductComponent component)
        => string.IsNullOrWhiteSpace(component.PayloadDirectoryName) ? component.Id : component.PayloadDirectoryName;

    private static InstallerComponentTarget? ComponentTarget(ProductComponent component)
        => component.Target
           ?? (Enum.TryParse<InstallerComponentTarget>(component.Id, ignoreCase: true, out var target) ? target : null);

    public static bool IsNixOsHost()
    {
        const string osReleasePath = "/etc/os-release";
        if (!File.Exists(osReleasePath))
            return false;

        var lines = File.ReadAllLines(osReleasePath);
        return lines.Any(line =>
            line.Equals("ID=nixos", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("ID=\"nixos\"", StringComparison.OrdinalIgnoreCase));
    }

    private static InstallerAdapterComposition Composition()
    {
        var commands = new ProcessInstallerCommandRunner();
        return new InstallerAdapterComposition(
            commands,
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));
    }

}

internal sealed record InstallerAdapterComposition(
    ICommandRunner Commands,
    IRequiredCommandRunner RequiredCommands,
    DockerPrerequisite DockerPrerequisite);
