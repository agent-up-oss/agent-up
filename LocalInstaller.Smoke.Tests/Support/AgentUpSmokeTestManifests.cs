using LocalInstaller.Smoke.Features.InstalledServiceValidation.Models;

namespace LocalInstaller.Smoke.Tests.Support;

internal static class AgentUpSmokeTestManifests
{
    private const string ProductName = "Agent-Up";
    private const string ProductSlug = "agent-up";

    public static SmokeProductConfig Product()
        => new(
            ServiceName: ProductSlug + "-server",
            CliShimName: ProductSlug,
            ArtifactBaseName: ProductSlug,
            DisplayName: ProductName,
            InstallDirName: ProductName,
            WorkspaceConfigFileName: "agent-up.json",
            ServerUrlEnvironmentVariable: "AGENTUP_SERVER_URL",
            InstallerExecutableName: "LocalInstaller.App",
            DesktopExecutableName: "AgentUp.Desktop",
            ServerExecutableName: "AgentUp.Server",
            CliExecutableName: "AgentUp.CLI",
            TrayExecutableName: "AgentUp.Tray");
}
