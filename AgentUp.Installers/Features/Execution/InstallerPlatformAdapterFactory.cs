using AgentUp.Installers.Features.MacOs;
using AgentUp.Installers.Features.Ubuntu;
using AgentUp.Installers.Features.Windows;

namespace AgentUp.Installers.Features.Execution;

public static class InstallerPlatformAdapterFactory
{
    public const string FakeInstallerVariable = "AGENTUP_INSTALLER_FAKE";
    public const string PayloadRootVariable = "AGENTUP_INSTALLER_PAYLOAD_ROOT";

    public static IInstallerPlatformAdapter Create()
    {
        if (Environment.GetEnvironmentVariable(FakeInstallerVariable) == "1")
            return new FakeInstallerPlatformAdapter(CurrentPlatformName() + " dry run");

        var payloadRoot = Environment.GetEnvironmentVariable(PayloadRootVariable);
        if (string.IsNullOrWhiteSpace(payloadRoot))
            throw new InvalidOperationException($"{PayloadRootVariable} must point at a payload root containing desktop, server, and cli directories.");

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

    private static IInstallerPlatformAdapter CreateUbuntuAdapter(string payloadRoot)
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var payload = new UbuntuInstallPayload(
            DesktopDirectory: System.IO.Path.Join(payloadRoot, "desktop"),
            ServerDirectory: System.IO.Path.Join(payloadRoot, "server"),
            CliDirectory: System.IO.Path.Join(payloadRoot, "cli"),
            ServiceFilePath: System.IO.Path.Join(repositoryRoot, "packaging", "linux", "agent-up-server.service"),
            IconPath: System.IO.Path.Join(repositoryRoot, "media", "logo.png"));

        return new UbuntuInstallerPlatformAdapter(
            new ProcessInstallerCommandRunner(),
            new UbuntuInstallerFileSystem(),
            new UbuntuInstallerOptions(payload, UbuntuInstallerPaths.SystemDefault()));
    }

    private static IInstallerPlatformAdapter CreateMacOsAdapter(string payloadRoot)
    {
        var payload = new MacOsInstallPayload(
            DesktopDirectory: System.IO.Path.Join(payloadRoot, "desktop"),
            ServerDirectory: System.IO.Path.Join(payloadRoot, "server"),
            CliDirectory: System.IO.Path.Join(payloadRoot, "cli"));

        return new MacOsInstallerPlatformAdapter(
            new ProcessInstallerCommandRunner(),
            new MacOsInstallerFileSystem(),
            new MacOsInstallerOptions(payload, MacOsInstallerPaths.SystemDefault()));
    }

    private static IInstallerPlatformAdapter CreateWindowsAdapter(string payloadRoot)
    {
        var payload = new WindowsInstallPayload(
            DesktopDirectory: System.IO.Path.Join(payloadRoot, "desktop"),
            ServerDirectory: System.IO.Path.Join(payloadRoot, "server"),
            CliDirectory: System.IO.Path.Join(payloadRoot, "cli"));

        return new WindowsInstallerPlatformAdapter(
            new ProcessInstallerCommandRunner(),
            new WindowsInstallerFileSystem(),
            new WindowsInstallerOptions(payload, WindowsInstallerPaths.SystemDefault()));
    }

    private static string CurrentPlatformName()
    {
        if (OperatingSystem.IsWindows())
            return "Windows";
        if (OperatingSystem.IsMacOS())
            return "macOS";
        return "Linux";
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
