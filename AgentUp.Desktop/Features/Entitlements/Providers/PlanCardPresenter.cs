using AgentUp.Desktop.Features.Entitlements.DTOs;
using AgentUp.Desktop.Features.Entitlements.Models;

namespace AgentUp.Desktop.Features.Entitlements.Providers;

public static class PlanCardPresenter
{
    public const string WorkspaceCreateFeature = "workspace.create";
    public const string WorkspaceCreateUnavailableMessage =
        "This Server does not allow adding workspaces from the client.";

    public static PlanCard Present(EntitlementsDocumentDto? document)
    {
        if (document is null)
        {
            return new PlanCard(
                "Plan unavailable",
                "",
                "The Server did not return an entitlement document.",
                false,
                [],
                []);
        }

        var features = (document.Features ?? new Dictionary<string, EntitlementFeatureDocument>())
            .Select(pair => new PlanCardFeature(pair.Key, pair.Value.Available))
            .ToArray();
        var limits = (document.Limits ?? new Dictionary<string, EntitlementLimitDocument>())
            .Select(pair => new PlanCardLimit(pair.Key, pair.Value.Max, pair.Value.Used))
            .ToArray();
        var enabled = features.Count(feature => feature.Available);
        return new PlanCard(
            document.DisplayName ?? "Plan",
            document.Billing ?? "",
            $"{enabled} of {features.Length} operations available",
            true,
            features,
            limits);
    }

    public static bool IsFeatureAvailable(EntitlementsDocumentDto? document, string feature)
        => document?.Features is { } features
           && features.TryGetValue(feature, out var record)
           && record.Available;
}
