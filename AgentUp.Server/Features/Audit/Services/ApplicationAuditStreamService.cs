using System.Text.Json;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.Audit.Services;

public sealed class ApplicationAuditStreamService(AuditEventBus eventBus)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteAsync(
        HttpResponse response,
        string workspaceId,
        string application,
        IReadOnlyList<string>? kinds,
        IReadOnlyList<string>? streams,
        CancellationToken cancellationToken)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-store";
        response.Headers.Connection = "keep-alive";

        await using var subscription = eventBus.Subscribe(evt =>
            AuditEventMatching.MatchesStreamSubscription(workspaceId, application, kinds, streams, evt));
        try
        {
            await foreach (var evt in subscription.Reader.ReadAllAsync(cancellationToken))
            {
                var payload = JsonSerializer.Serialize(ToDto(evt), JsonOptions);
                await response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private static AuditEventDto ToDto(AuditEvent evt) =>
        new(
            evt.EventId,
            evt.Timestamp,
            evt.Kind,
            evt.Source,
            evt.Action,
            evt.Outcome,
            evt.WorkspaceId,
            evt.RepositoryPath,
            evt.WorktreePath,
            evt.WorkdirId,
            evt.Branch,
            evt.Commit,
            evt.Dirty,
            evt.Details,
            evt.ArtifactIds);
}
