namespace AgentUp.Installers.Features.WindowsInstallation.Models;

public sealed partial record WindowsInstallerManifest
{
    public const string DefaultCliShimName = "agent-up.cmd";
    private const string AgentUpUpgradeCode = "5E8FB224-E5E3-4D48-8B62-2F50D521CBB0";

    public static WindowsInstallerManifest Create(string version, string serverUrl)
        => new(
            ProductName: "Agent-Up",
            Manufacturer: "Agent-Up",
            Version: version,
            UpgradeCode: AgentUpUpgradeCode,
            ServiceName: "agent-up-server",
            CliShimName: DefaultCliShimName,
            BundleName: "Agent-Up",
            ServerUrl: serverUrl)
        {
            GuidSeedScope = "Agent-Up Windows Installer",
            UsesLegacyGuidScope = true
        };
}
