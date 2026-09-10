namespace AgentUp.Server.Features.Audit.Providers;

internal static class AuditEventLogLinePrefilter
{
    internal static bool MightMatch(DTOs.AuditEventQuery query, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (!string.IsNullOrWhiteSpace(query.WorkspaceId)
            && !ContainsJsonStringValue(line, "WorkspaceId", query.WorkspaceId))
            return false;

        if (!string.IsNullOrWhiteSpace(query.Application)
            && !ContainsApplication(line, query.Application))
            return false;

        if (query.Kinds is { Count: > 0 })
        {
            if (!query.Kinds.Any(kind => ContainsJsonStringValue(line, "Kind", kind)))
                return false;
        }
        else if (!string.IsNullOrWhiteSpace(query.Kind)
                 && !ContainsJsonStringValue(line, "Kind", query.Kind))
            return false;

        if (query.Streams is { Count: > 0 }
            && ContainsJsonStringValue(line, "Kind", "application")
            && !query.Streams.Any(stream => ContainsJsonStringValue(line, "stream", stream)))
            return false;

        return true;
    }

    private static bool ContainsApplication(string line, string application)
        => ContainsJsonStringValue(line, "application", application)
           || ContainsJsonStringValue(line, "applicationName", application)
           || ContainsJsonStringValue(line, "appName", application);

    private static bool ContainsJsonStringValue(string line, string key, string value)
        => line.Contains($"\"{key}\":\"{value}\"", StringComparison.OrdinalIgnoreCase);
}
