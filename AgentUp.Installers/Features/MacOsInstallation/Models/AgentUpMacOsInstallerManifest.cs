namespace AgentUp.Installers.Features.MacOsInstallation.Models;

public sealed partial record MacOsInstallerManifest
{
    public static MacOsInstallerManifest Create(string version)
        => new(
            ProductName: "Agent-Up",
            DesktopBundleIdentifier: "dev.agent-up.desktop",
            InstallerBundleIdentifier: "dev.agent-up.installer",
            ServerLaunchDaemonLabel: "dev.agent-up.server",
            TrayLaunchAgentLabel: "dev.agent-up.tray",
            BundleIconFile: "Agent-Up.png",
            Version: version,
            ServerUrl: "http://127.0.0.1:5000");
}
