using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.Core.Tests.Support;

internal static class AgentUpTestManifests
{
    private const string ProductName = "Agent-Up";
    private const string ProductSlug = "agent-up";
    private const string EnvironmentPrefix = "AGENTUP";
    private const string WindowsUpgradeCode = "8f9076cc-95f1-4bd6-a087-1686e5e0d540";

    public static ProductManifest Product()
        => new(ProductName, ProductSlug, EnvironmentPrefix)
        {
            Components =
            [
                new("desktop", "Desktop", "Desktop app.", InstallerComponentTarget.Desktop, "AgentUp.Desktop", "desktop"),
                new("server", "Server", "Server app.", InstallerComponentTarget.Server, "AgentUp.Server", "server"),
                new("cli", "CLI", "CLI app.", InstallerComponentTarget.Cli, "AgentUp.CLI", "cli"),
                new("tray", "Tray", "Tray app.", InstallerComponentTarget.Tray, "AgentUp.Tray", "tray")
            ],
            WindowsUpgradeCode = WindowsUpgradeCode
        };

    public static PayloadSelection BundledPayload(Version version)
        => PayloadSelection.Bundled(ProductName, version);
}
