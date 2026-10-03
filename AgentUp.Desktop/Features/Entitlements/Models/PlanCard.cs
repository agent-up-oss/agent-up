namespace AgentUp.Desktop.Features.Entitlements.Models;

public sealed record PlanCard(
    string DisplayName,
    string Billing,
    string Summary,
    bool Available,
    IReadOnlyList<PlanCardFeature> Features,
    IReadOnlyList<PlanCardLimit> Limits);
