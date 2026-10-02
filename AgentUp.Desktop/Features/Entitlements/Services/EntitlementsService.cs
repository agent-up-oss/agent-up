using AgentUp.Desktop.Features.Entitlements.DTOs;
using AgentUp.Desktop.Features.Entitlements.Providers;

namespace AgentUp.Desktop.Features.Entitlements.Services;

public sealed class EntitlementsService(EntitlementsApiClient client)
{
    public Task<EntitlementsDocumentDto?> GetAsync(CancellationToken cancellationToken = default)
        => client.GetAsync(cancellationToken);

    public async Task<PlanLoadResult> GetPlanAsync(CancellationToken cancellationToken = default)
    {
        var document = await client.GetAsync(cancellationToken);
        if (document is null)
            return new PlanLoadResult(null, true);

        return new PlanLoadResult(
            PlanCardPresenter.Present(document),
            PlanCardPresenter.IsFeatureAvailable(document, PlanCardPresenter.WorkspaceCreateFeature));
    }
}
