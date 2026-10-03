namespace AgentUp.Desktop.Features.Authentication.Models;

public sealed record RecommendedServer(string Id, string Url, string DisplayName)
{
    public const string RecommendedId = "recommended";
    public const string DefaultDisplayName = "Agent-Up Cloud";
}
