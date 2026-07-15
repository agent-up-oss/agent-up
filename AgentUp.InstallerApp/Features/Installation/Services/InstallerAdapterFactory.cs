using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Ubuntu;

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
            new UbuntuInstallerCommandRunner(),
            new UbuntuInstallerFileSystem(),
            new UbuntuInstallerOptions(payload, UbuntuInstallerPaths.SystemDefault()));
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
