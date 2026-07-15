using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.MacOs;
using AgentUp.Installers.Features.Ubuntu;
using AgentUp.Installers.Features.Windows;

namespace AgentUp.InstallerApp.Features.Installation.Services;

internal static class InstallerAdapterFactory
{
    public static IInstallerPlatformAdapter Create()
    {
        if (OperatingSystem.IsLinux()
            && Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_REAL_UBUNTU") == "1"
            && TryCreateUbuntuAdapter() is { } ubuntu)
        {
            return ubuntu;
        }

        if (OperatingSystem.IsMacOS()
            && Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_REAL_MACOS") == "1"
            && TryCreateMacOsAdapter() is { } macOs)
        {
            return macOs;
        }

        if (OperatingSystem.IsWindows()
            && Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_REAL_WINDOWS") == "1"
            && TryCreateWindowsAdapter() is { } windows)
        {
            return windows;
        }

        return new FakeInstallerPlatformAdapter(CurrentPlatformName());
    }

    private static IInstallerPlatformAdapter? TryCreateUbuntuAdapter()
    {
        var payloadRoot = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT");
        if (string.IsNullOrWhiteSpace(payloadRoot))
            return null;

        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var payload = new UbuntuInstallPayload(
            DesktopDirectory: Path.Combine(payloadRoot, "desktop"),
            ServerDirectory: Path.Combine(payloadRoot, "server"),
            CliDirectory: Path.Combine(payloadRoot, "cli"),
            ServiceFilePath: Path.Combine(repositoryRoot, "packaging", "linux", "agent-up-server.service"),
            IconPath: Path.Combine(repositoryRoot, "media", "logo.png"));

        return new UbuntuInstallerPlatformAdapter(
            new ProcessInstallerCommandRunner(),
            new UbuntuInstallerFileSystem(),
            new UbuntuInstallerOptions(payload, UbuntuInstallerPaths.SystemDefault()));
    }

    private static IInstallerPlatformAdapter? TryCreateMacOsAdapter()
    {
        var payloadRoot = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT");
        if (string.IsNullOrWhiteSpace(payloadRoot))
            return null;

        var payload = new MacOsInstallPayload(
            DesktopDirectory: Path.Combine(payloadRoot, "desktop"),
            ServerDirectory: Path.Combine(payloadRoot, "server"),
            CliDirectory: Path.Combine(payloadRoot, "cli"));

        return new MacOsInstallerPlatformAdapter(
            new ProcessInstallerCommandRunner(),
            new MacOsInstallerFileSystem(),
            new MacOsInstallerOptions(payload, MacOsInstallerPaths.SystemDefault()));
    }

    private static IInstallerPlatformAdapter? TryCreateWindowsAdapter()
    {
        var payloadRoot = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT");
        if (string.IsNullOrWhiteSpace(payloadRoot))
            return null;

        var payload = new WindowsInstallPayload(
            DesktopDirectory: Path.Combine(payloadRoot, "desktop"),
            ServerDirectory: Path.Combine(payloadRoot, "server"),
            CliDirectory: Path.Combine(payloadRoot, "cli"));

        return new WindowsInstallerPlatformAdapter(
            new ProcessInstallerCommandRunner(),
            new WindowsInstallerFileSystem(),
            new WindowsInstallerOptions(payload, WindowsInstallerPaths.SystemDefault()));
    }

    private static string CurrentPlatformName()
    {
        if (OperatingSystem.IsWindows())
            return "Windows dry run";
        if (OperatingSystem.IsMacOS())
            return "macOS dry run";
        return "Linux dry run";
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
