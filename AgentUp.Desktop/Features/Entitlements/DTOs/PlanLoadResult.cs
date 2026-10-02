using AgentUp.Desktop.Features.Entitlements.Models;

namespace AgentUp.Desktop.Features.Entitlements.DTOs;

public sealed record PlanLoadResult(PlanCard? Card, bool CanCreateWorkspace);
