namespace AgentUp.Desktop.Features.Entitlements.Models;

public sealed record PlanCard(
    string DisplayName,
    string Billing,
    string Summary,
    bool Available,
    IReadOnlyList<PlanCardFeature> Features,
    IReadOnlyList<PlanCardLimit> Limits)
{
    public IReadOnlyList<PlanCardFeature> RestrictedFeatures
        => Features.Where(feature => !feature.Available).ToArray();

    public bool HasRestrictions => RestrictedFeatures.Count > 0;
}
