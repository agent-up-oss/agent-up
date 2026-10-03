using AgentUp.Desktop.Features.Entitlements.DTOs;
using AgentUp.Desktop.Features.Entitlements.Services;

namespace AgentUp.Desktop.Features.Entitlements.Controllers;

public sealed class EntitlementsController(EntitlementsService entitlements)
{
    public Task<EntitlementsDocumentDto?> GetAsync(CancellationToken cancellationToken = default)
        => entitlements.GetAsync(cancellationToken);

    public Task<PlanLoadResult> GetPlanAsync(CancellationToken cancellationToken = default)
        => entitlements.GetPlanAsync(cancellationToken);
}
