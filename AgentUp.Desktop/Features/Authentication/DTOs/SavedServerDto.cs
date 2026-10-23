using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Authentication.DTOs;

public sealed record SavedServerDto(
    string Id,
    string Url,
    bool HasCredential,
    bool IsActive,
    string DisplayName,
    bool CanRemove,
    bool IsFake,
    bool IsRecommended)
{
    public string SelectAutomationId => DesktopAutomationIds.LoginSaved(Id);
    public string RemoveAutomationId => DesktopAutomationIds.LoginRemoveSaved(Id);
}
