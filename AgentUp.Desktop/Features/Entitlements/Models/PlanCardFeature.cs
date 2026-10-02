namespace AgentUp.Desktop.Features.Entitlements.Models;

public sealed record PlanCardFeature(string Id, bool Available)
{
    public string Label => $"{Id}: {(Available ? "available" : "unavailable")}";
}
