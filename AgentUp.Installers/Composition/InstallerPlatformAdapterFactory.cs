using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.MacOsInstallation.DTOs;
using AgentUp.Installers.Features.MacOsInstallation.Models;
using AgentUp.Installers.Features.MacOsInstallation.Providers;
using AgentUp.Installers.Features.NixOsInstallation.Providers;
using AgentUp.Installers.Features.UbuntuInstallation.DTOs;
using AgentUp.Installers.Features.UbuntuInstallation.Models;
using AgentUp.Installers.Features.UbuntuInstallation.Providers;
using AgentUp.Installers.Features.WindowsInstallation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using AgentUp.Installers.Features.WindowsInstallation.Providers;

namespace AgentUp.Installers.Composition;

public static class InstallerPlatformAdapterFactory
{
    public const string FakeInstallerVariable = "AGENTUP_INSTALLER_FAKE";
    public const string PayloadRootVariable = "AGENTUP_INSTALLER_PAYLOAD_ROOT";
    public const string NixOsLookupOnlyVariable = "AGENTUP_INSTALLER_NIXOS_LOOKUP_ONLY";

    public static IInstallerPlatformAdapter Create()
    {
        if (Environment.GetEnvironmentVariable(FakeInstallerVariable) == "1")
            return new FakeInstallerPlatformAdapter(CurrentPlatformName() + " dry run");

        if (OperatingSystem.IsLinux())
        {
            if (UseNixOsLookupOnlyMode())
                return CreateNixOsAdapter();

            var payloadRoot = ResolvePayloadRoot(AppContext.BaseDirectory);
            return CreateUbuntuAdapter(payloadRoot);
        }
        if (OperatingSystem.IsMacOS())
        {
            var payloadRoot = ResolvePayloadRoot(AppContext.BaseDirectory);
            return CreateMacOsAdapter(payloadRoot);
        }
        if (OperatingSystem.IsWindows())
        {
            var payloadRoot = ResolvePayloadRoot(AppContext.BaseDirectory);
            return CreateWindowsAdapter(payloadRoot);
        }

        throw new PlatformNotSupportedException("Agent-Up installer does not support this operating system.");
    }

    public static IInstallerPlatformAdapter CreateFake(string platformName)
        => new FakeInstallerPlatformAdapter(platformName);

    public static string ResolvePayloadRoot(string appBaseDirectory)
        => ResolvePayloadRoot(appBaseDirectory, ProductManifest.AgentUp());

    public static string ResolvePayloadRoot(string appBaseDirectory, ProductManifest manifest)
    {
        var payloadRoot = Environment.GetEnvironmentVariable(manifest.PayloadRootVariable);
        if (!string.IsNullOrWhiteSpace(payloadRoot))
            return payloadRoot;

        foreach (var candidateDirectory in PayloadCandidateDirectories(appBaseDirectory))
        {
            var bundledPayloadRoot = System.IO.Path.Join(candidateDirectory, "payload");
            if (IsPayloadRoot(bundledPayloadRoot))
                return bundledPayloadRoot;
        }

        throw new InvalidOperationException($"{manifest.PayloadRootVariable} must point at a payload root containing desktop, server, and cli directories, or the installer app must include a bundled payload directory next to the executable.");
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

    private static bool IsPayloadRoot(string payloadRoot)
        => Directory.Exists(System.IO.Path.Join(payloadRoot, "desktop")) &&
           Directory.Exists(System.IO.Path.Join(payloadRoot, "server")) &&
           Directory.Exists(System.IO.Path.Join(payloadRoot, "cli"));

    private static void AddCandidate(List<string> candidates, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        var fullPath = System.IO.Path.GetFullPath(candidate);
        if (!candidates.Contains(fullPath, StringComparer.Ordinal))
            candidates.Add(fullPath);
    }

    private static IInstallerPlatformAdapter CreateUbuntuAdapter(string payloadRoot)
    {
        var composition = Composition();
        var manifest = UbuntuInstallerManifest.AgentUp();
        var paths = UbuntuInstallerPaths.ForProduct(manifest);
        var payload = new UbuntuInstallPayload(
            DesktopDirectory: System.IO.Path.Join(payloadRoot, "desktop"),
            ServerDirectory: System.IO.Path.Join(payloadRoot, "server"),
            CliDirectory: System.IO.Path.Join(payloadRoot, "cli"),
            ServiceFilePath: System.IO.Path.Join(payloadRoot, "service", manifest.ServiceUnitName),
            IconPath: System.IO.Path.Join(payloadRoot, "icon", "Agent-Up.png"));

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

    private static IInstallerPlatformAdapter CreateMacOsAdapter(string payloadRoot)
    {
        var composition = Composition();
        var payload = new MacOsInstallPayload(
            DesktopDirectory: System.IO.Path.Join(payloadRoot, "desktop"),
            ServerDirectory: System.IO.Path.Join(payloadRoot, "server"),
            CliDirectory: System.IO.Path.Join(payloadRoot, "cli"),
            IconPath: System.IO.Path.Join(payloadRoot, "icon", "Agent-Up.png"));

        return new MacOsInstallerPlatformAdapter(
            composition.Commands,
            new MacOsInstallerFileSystem(),
            new MacOsInstallerOptions(payload),
            composition.RequiredCommands,
            composition.DockerPrerequisite);
    }

    private static IInstallerPlatformAdapter CreateWindowsAdapter(string payloadRoot)
    {
        var composition = Composition();
        var payload = new WindowsInstallPayload(
            DesktopDirectory: System.IO.Path.Join(payloadRoot, "desktop"),
            ServerDirectory: System.IO.Path.Join(payloadRoot, "server"),
            CliDirectory: System.IO.Path.Join(payloadRoot, "cli"));

        return new WindowsInstallerPlatformAdapter(
            composition.Commands,
            new WindowsInstallerFileSystem(),
            new WindowsInstallerOptions(payload, WindowsInstallerPaths.SystemDefault()),
            composition.RequiredCommands,
            composition.DockerPrerequisite);
    }

    private static string CurrentPlatformName()
    {
        if (OperatingSystem.IsLinux() && UseNixOsLookupOnlyMode())
            return "NixOS";
        if (OperatingSystem.IsWindows())
            return "Windows";
        if (OperatingSystem.IsMacOS())
            return "macOS";
        return "Linux";
    }

    public static bool UseNixOsLookupOnlyMode()
        => Environment.GetEnvironmentVariable(NixOsLookupOnlyVariable) == "1" || IsNixOsHost();

    private static bool IsNixOsHost()
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
