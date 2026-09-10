using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Audit.Providers;

internal static class AuditApplicationNameResolver
{
    internal static bool TryGetApplication(AuditEvent evt, out string application)
    {
        if (TryGetDetail(evt.Details, "application", out application)
            || TryGetDetail(evt.Details, "applicationName", out application)
            || TryGetDetail(evt.Details, "appName", out application))
            return true;

        application = string.Empty;
        return false;
    }

    private static bool TryGetDetail(
        IReadOnlyDictionary<string, string> details,
        string key,
        out string application)
    {
        if (details.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            application = value;
            return true;
        }

        application = string.Empty;
        return false;
    }
}
