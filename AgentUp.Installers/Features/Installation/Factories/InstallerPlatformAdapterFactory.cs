using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
using AgentUp.Installers.Features.MacOsInstallation.DTOs;
using AgentUp.Installers.Features.MacOsInstallation.Models;
using AgentUp.Installers.Features.MacOsInstallation.Providers;
using AgentUp.Installers.Features.UbuntuInstallation.DTOs;
using AgentUp.Installers.Features.UbuntuInstallation.Models;
using AgentUp.Installers.Features.UbuntuInstallation.Providers;
using AgentUp.Installers.Features.WindowsInstallation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using AgentUp.Installers.Features.WindowsInstallation.Providers;

namespace AgentUp.Installers.Features.Installation.Factories;

public static class InstallerPlatformAdapterFactory
{
    public const string FakeInstallerVariable = "AGENTUP_INSTALLER_FAKE";
    public const string PayloadRootVariable = "AGENTUP_INSTALLER_PAYLOAD_ROOT";

    public static IInstallerPlatformAdapter Create()
    {
        if (Environment.GetEnvironmentVariable(FakeInstallerVariable) == "1")
            return new FakeInstallerPlatformAdapter(CurrentPlatformName() + " dry run");

        var payloadRoot = ResolvePayloadRoot(AppContext.BaseDirectory);

        if (OperatingSystem.IsLinux())
            return CreateUbuntuAdapter(payloadRoot);
        if (OperatingSystem.IsMacOS())
            return CreateMacOsAdapter(payloadRoot);
        if (OperatingSystem.IsWindows())
            return CreateWindowsAdapter(payloadRoot);

        throw new PlatformNotSupportedException("Agent-Up installer does not support this operating system.");
    }

    public static IInstallerPlatformAdapter CreateFake(string platformName)
        => new FakeInstallerPlatformAdapter(platformName);

    public static string ResolvePayloadRoot(string appBaseDirectory)
    {
        var payloadRoot = Environment.GetEnvironmentVariable(PayloadRootVariable);
        if (!string.IsNullOrWhiteSpace(payloadRoot))
            return payloadRoot;

        foreach (var candidateDirectory in PayloadCandidateDirectories(appBaseDirectory))
        {
            var bundledPayloadRoot = System.IO.Path.Join(candidateDirectory, "payload");
            if (IsPayloadRoot(bundledPayloadRoot))
                return bundledPayloadRoot;
        }

        throw new InvalidOperationException($"{PayloadRootVariable} must point at a payload root containing desktop, server, and cli directories, or the installer app must include a bundled payload directory next to the executable.");
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
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var payload = new UbuntuInstallPayload(
            DesktopDirectory: System.IO.Path.Join(payloadRoot, "desktop"),
            ServerDirectory: System.IO.Path.Join(payloadRoot, "server"),
            CliDirectory: System.IO.Path.Join(payloadRoot, "cli"),
            ServiceFilePath: System.IO.Path.Join(repositoryRoot, "packaging", "linux", "agent-up-server.service"),
            IconPath: System.IO.Path.Join(repositoryRoot, "media", "logo.png"));

        return new UbuntuInstallerPlatformAdapter(
            composition.Commands,
            new UbuntuInstallerFileSystem(),
            new UbuntuInstallerOptions(payload, UbuntuInstallerPaths.SystemDefault()),
            composition.RequiredCommands,
            composition.DockerPrerequisite);
    }

    private static IInstallerPlatformAdapter CreateMacOsAdapter(string payloadRoot)
    {
        var composition = Composition();
        var payload = new MacOsInstallPayload(
            DesktopDirectory: System.IO.Path.Join(payloadRoot, "desktop"),
            ServerDirectory: System.IO.Path.Join(payloadRoot, "server"),
            CliDirectory: System.IO.Path.Join(payloadRoot, "cli"));

        return new MacOsInstallerPlatformAdapter(
            composition.Commands,
            new MacOsInstallerFileSystem(),
            new MacOsInstallerOptions(payload, MacOsInstallerPaths.SystemDefault()),
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
        if (OperatingSystem.IsWindows())
            return "Windows";
        if (OperatingSystem.IsMacOS())
            return "macOS";
        return "Linux";
    }

    private static InstallerAdapterComposition Composition()
    {
        var commands = new ProcessInstallerCommandRunner();
        return new InstallerAdapterComposition(
            commands,
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Join(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}

internal sealed record InstallerAdapterComposition(
    ICommandRunner Commands,
    IRequiredCommandRunner RequiredCommands,
    DockerPrerequisite DockerPrerequisite);
