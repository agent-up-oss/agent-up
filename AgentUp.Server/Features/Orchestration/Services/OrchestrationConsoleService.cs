using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Orchestration.Services;

public sealed class OrchestrationConsoleService(
    WorkspaceQueryController workspaces,
    ProcessesController processes,
    AuditController audit,
    ILogger<OrchestrationConsoleService> logger)
{
    private const int DefaultLineLimit = 200;
    private const int DefaultAuditLimit = 100;
    private const int MaxLineLimit = 1000;
    private const int MaxAuditLimit = 500;

    public async Task<McpToolResult> GetConsoleAsync(
        string? id,
        string? worktreePath,
        int lineLimit,
        int auditLimit,
        CancellationToken cancellationToken)
    {
        var workspace = ResolveWorkspace(id, worktreePath);
        if (workspace is null)
            return new McpToolResult(false, "Workspace not found.");

        var normalizedLineLimit = NormalizeLimit(lineLimit, DefaultLineLimit, MaxLineLimit);
        var normalizedAuditLimit = NormalizeLimit(auditLimit, DefaultAuditLimit, MaxAuditLimit);
        var applications = new List<ApplicationConsoleSnapshot>();

        foreach (var app in workspace.Applications)
        {
            var lines = await processes.GetOutputAsync(workspace.Id, app.Name);
            var visibleLines = lines
                .Skip(Math.Max(0, lines.Count - normalizedLineLimit))
                .ToList();
            applications.Add(new ApplicationConsoleSnapshot(
                app.Name,
                app.State.ToString(),
                lines.Count,
                lines.Count > visibleLines.Count,
                visibleLines));
        }

        var auditEvents = await audit.QueryAsync(
            new AuditEventQuery(
                workspace.Id,
                null,
                null,
                null,
                null,
                "application",
                "process",
                null,
                null,
                null,
                normalizedAuditLimit),
            cancellationToken);
        var auditTrail = auditEvents
            .Where(evt => string.Equals(evt.Action, "application_console_line", StringComparison.Ordinal))
            .OrderBy(evt => evt.Timestamp)
            .Select(ToConsoleAuditEvent)
            .ToList();

        await RecordSnapshotAuditEventAsync(workspace.Id, applications, auditTrail.Count, cancellationToken);

        return new McpToolResult(
            true,
            $"Returned console snapshot for workspace \"{workspace.DisplayName}\".",
            new WorkspaceConsoleSnapshot(
                workspace.Id,
                workspace.DisplayName,
                DateTimeOffset.UtcNow,
                applications,
                auditTrail));
    }

    private Workspace? ResolveWorkspace(string? id, string? worktreePath)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return workspaces.GetById(id);

        if (string.IsNullOrWhiteSpace(worktreePath))
            return null;

        return workspaces.GetAll().FirstOrDefault(w =>
            string.Equals(w.WorktreePath, worktreePath, StringComparison.OrdinalIgnoreCase));
    }

    private static int NormalizeLimit(int value, int defaultValue, int maxValue)
        => Math.Clamp(value <= 0 ? defaultValue : value, 1, maxValue);

    private static WorkspaceConsoleAuditEvent ToConsoleAuditEvent(AuditEvent evt)
    {
        evt.Details.TryGetValue("applicationName", out var applicationName);
        evt.Details.TryGetValue("stream", out var stream);
        evt.Details.TryGetValue("message", out var message);

        return new WorkspaceConsoleAuditEvent(
            evt.EventId,
            evt.Timestamp,
            evt.Kind,
            evt.Source,
            evt.Action,
            evt.Outcome,
            applicationName,
            stream,
            message);
    }

    private async Task RecordSnapshotAuditEventAsync(
        string workspaceId,
        IReadOnlyList<ApplicationConsoleSnapshot> applications,
        int auditEventCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await audit.RecordAsync(
                new AuditRecordRequest(
                    "workspace",
                    "mcp",
                    "workspace_console_snapshot",
                    "success",
                    workspaceId,
                    new Dictionary<string, string>
                    {
                        ["applicationCount"] = applications.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["lineCount"] = applications.Sum(app => app.Lines.Count).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["auditEventCount"] = auditEventCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }),
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Failed to record console snapshot audit event");
        }
    }
}
