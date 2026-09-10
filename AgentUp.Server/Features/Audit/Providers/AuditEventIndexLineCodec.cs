using System.Text.Json;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Audit.Providers;

internal static class AuditEventIndexLineCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static string Format(AuditEvent evt, long offset, int length)
    {
        string? stream = null;
        if (string.Equals(evt.Kind, "application", StringComparison.OrdinalIgnoreCase)
            && evt.Details.TryGetValue("stream", out var streamValue)
            && !string.IsNullOrWhiteSpace(streamValue))
            stream = streamValue;

        var payload = new AuditEventIndexLinePayload(
            evt.EventId,
            evt.Timestamp,
            evt.Kind,
            stream,
            evt.Scope,
            offset,
            length);

        return JsonSerializer.Serialize(payload, Options);
    }

    internal static bool TryParse(string line, out AuditEventIndexEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        try
        {
            var payload = JsonSerializer.Deserialize<AuditEventIndexLinePayload>(line, Options);
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.Id)
                || string.IsNullOrWhiteSpace(payload.Kind)
                || payload.Offset < 0
                || payload.Length <= 0)
                return false;

            entry = new AuditEventIndexEntry(
                payload.Id,
                payload.Timestamp,
                payload.Kind,
                payload.Stream,
                payload.Scope,
                payload.Offset,
                payload.Length);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
