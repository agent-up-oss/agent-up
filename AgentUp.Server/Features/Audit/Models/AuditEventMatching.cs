namespace AgentUp.Server.Features.Audit.Models;

internal static class AuditEventMatching
{
    public static bool MatchesApplication(string? application, AuditEvent evt)
        => string.IsNullOrWhiteSpace(application)
           || MatchesApplicationDetail(evt.Details, application, "application")
           || MatchesApplicationDetail(evt.Details, application, "applicationName")
           || MatchesApplicationDetail(evt.Details, application, "appName");

    public static bool MatchesKinds(IReadOnlyList<string>? kinds, AuditEvent evt)
        => MatchesKind(null, kinds, evt.Kind);

    public static bool MatchesKind(string? kind, IReadOnlyList<string>? kinds, string? actual)
    {
        if (kinds is { Count: > 0 })
            return kinds.Any(expected => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(kind)
               || string.Equals(kind, actual, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesStreams(IReadOnlyList<string>? streams, AuditEvent evt)
    {
        if (streams is not { Count: > 0 })
            return true;

        if (!string.Equals(evt.Kind, "application", StringComparison.OrdinalIgnoreCase))
            return true;

        return evt.Details.TryGetValue("stream", out var stream)
               && streams.Any(expected => string.Equals(expected, stream, StringComparison.OrdinalIgnoreCase));
    }

    public static bool MatchesWorkspace(string? workspaceId, AuditEvent evt)
        => string.IsNullOrWhiteSpace(workspaceId)
           || string.Equals(workspaceId, evt.WorkspaceId, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesStreamSubscription(
        string workspaceId,
        string application,
        IReadOnlyList<string>? kinds,
        IReadOnlyList<string>? streams,
        AuditEvent evt)
        => MatchesWorkspace(workspaceId, evt)
           && MatchesApplication(application, evt)
           && MatchesKinds(kinds, evt)
           && MatchesStreams(streams, evt);

    private static bool MatchesApplicationDetail(
        IReadOnlyDictionary<string, string> details,
        string application,
        string key)
        => details.TryGetValue(key, out var actual)
           && string.Equals(application, actual, StringComparison.OrdinalIgnoreCase);
}
