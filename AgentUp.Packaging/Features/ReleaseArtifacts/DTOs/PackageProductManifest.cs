namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed record PackageProductManifest(
    string ProductName,
    string Slug,
    string EnvironmentPrefix)
{
    private static readonly PackageProductManifest AgentUpManifest = new("Agent-Up", "agent-up", "AGENTUP")
    {
        Manufacturer = "Agent-Up",
        WindowsUpgradeCode = "5E8FB224-E5E3-4D48-8B62-2F50D521CBB0"
    };

    public string? Manufacturer { get; init; }
    public string? WindowsUpgradeCode { get; init; }
    public string? WindowsServiceName { get; init; }
    public string? WindowsCliShimName { get; init; }
    public string? WindowsServerUrl { get; init; }

    public static PackageProductManifest AgentUp()
        => AgentUpManifest;
}
