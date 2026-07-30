namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed partial record PackageProductManifest
{
    public static PackageProductManifest AgentUp()
        => new("Agent-Up", "agent-up", "AGENTUP")
        {
            Manufacturer = "Agent-Up",
            WindowsUpgradeCode = "5E8FB224-E5E3-4D48-8B62-2F50D521CBB0"
        };
}
