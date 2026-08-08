using System.ComponentModel;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Audit.Controllers;

[McpServerToolType]
public sealed class AuditMcpTools(AuditController audit)
{
    [McpServerTool(Name = "audit_query", Title = "Query Audit Events")]
    [Description("Query durable Agent-Up audit events by workspace, workdir, repository, branch, commit, kind, source, outcome, and time range. Returns compact summaries by default (no Details); set compact=false to include Details, or call audit_get_event with an eventId for a single full record.")]
    public async Task<McpToolResult> Query(
        string? workspaceId = null,
        string? workdirId = null,
        string? repositoryPath = null,
        string? branch = null,
        string? commit = null,
        string? kind = null,
        string? source = null,
        string? outcome = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 100,
        bool compact = true,
        CancellationToken cancellationToken = default)
    {
        var events = await audit.QueryAsync(
            new AuditEventQuery(workspaceId, workdirId, repositoryPath, branch, commit, kind, source, outcome, from, to, limit),
            cancellationToken);

        if (compact)
        {
            var slim = events.Select(e => new
            {
                e.EventId,
                e.Timestamp,
                e.Kind,
                e.Source,
                e.Action,
                e.Outcome,
                e.WorkspaceId,
                e.WorkdirId,
                e.Branch,
                e.Commit,
                e.ArtifactIds
            }).ToList();
            return new McpToolResult(true,
                $"Returned {slim.Count} audit event(s) (compact). Call audit_get_event with an eventId for full Details.",
                slim);
        }

        return new McpToolResult(true, $"Returned {events.Count} audit event(s).", events);
    }

    [McpServerTool(Name = "audit_timeline", Title = "Audit Timeline")]
    [Description("Return a compact chronological timeline of recent Agent-Up audit events for agent context.")]
    public async Task<McpToolResult> Timeline(
        string? workspaceId = null,
        string? workdirId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var events = await audit.QueryAsync(
            new AuditEventQuery(workspaceId, workdirId, null, null, null, null, null, null, null, null, limit),
            cancellationToken);
        var timeline = events
            .OrderBy(evt => evt.Timestamp)
            .Select(evt => new
            {
                evt.EventId,
                evt.Timestamp,
                evt.Kind,
                evt.Source,
                evt.Action,
                evt.Outcome,
                evt.WorkspaceId,
                evt.WorkdirId,
                evt.Branch,
                evt.Commit,
                evt.ArtifactIds
            })
            .ToList();
        return new McpToolResult(true, $"Returned {timeline.Count} audit timeline item(s).", timeline);
    }

    [McpServerTool(Name = "audit_get_event", Title = "Get Audit Event")]
    [Description("Return full details for one audit event by event id.")]
    public async Task<McpToolResult> GetEvent(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        var evt = await audit.GetEventAsync(eventId, cancellationToken);
        return evt is null
            ? new McpToolResult(false, $"Audit event '{eventId}' was not found.")
            : new McpToolResult(true, $"Audit event '{eventId}'.", evt);
    }

    [McpServerTool(Name = "audit_load_artifact", Title = "Load Audit Artifact")]
    [Description("Load a Server-managed audit artifact by opaque artifact id. Set includeImage=true to return base64 image data.")]
    public async Task<McpToolResult> LoadArtifact(
        string artifactId,
        bool includeImage = true,
        CancellationToken cancellationToken = default)
    {
        var artifact = await audit.LoadArtifactAsync(artifactId, includeImage, cancellationToken);
        return artifact is null
            ? new McpToolResult(false, $"Audit artifact '{artifactId}' was not found.")
            : new McpToolResult(true, $"Audit artifact '{artifactId}'.", artifact);
    }
}
