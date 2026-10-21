namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// The closed set of toolchain ids a check may name in <c>requires</c>.
/// Adding one is a reviewed change to this list, not a free-form executable name.
/// </summary>
public static class ToolchainCatalog
{
    public static readonly IReadOnlyList<string> Ids = ["dotnet", "node", "npm", "docker", "git", "nix"];

    public static bool IsKnown(string id)
        => Ids.Contains(id, StringComparer.Ordinal);

    public static string Describe()
        => string.Join(", ", Ids);
}
