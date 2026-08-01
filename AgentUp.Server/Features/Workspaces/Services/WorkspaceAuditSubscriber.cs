using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceAuditSubscriber(
    WorkspaceEventBus workspaceEvents,
    AuditController audit,
    Action? onSubscribed = null) : BackgroundService
{
    private readonly Dictionary<string, string> _lastFingerprints = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sub = workspaceEvents.Subscribe();
        onSubscribed?.Invoke();
        await foreach (var evt in sub.Reader.ReadAllAsync(stoppingToken))
        {
            var applications = string.Join(',', evt.Applications.Select(app => $"{app.Name}:{app.State}"));
            var fingerprint = $"{evt.State}|{applications}";
            if (_lastFingerprints.TryGetValue(evt.WorkspaceId, out var previous)
                && string.Equals(previous, fingerprint, StringComparison.Ordinal))
                continue;

            try
            {
                await audit.RecordAsync(
                    new AuditRecordRequest(
                        "workspace",
                        "server",
                        "workspace_state_changed",
                        evt.State,
                        evt.WorkspaceId,
                        new Dictionary<string, string>
                        {
                            ["scope"] = "workspace",
                            ["applications"] = applications
                        }),
                    stoppingToken);
                _lastFingerprints[evt.WorkspaceId] = fingerprint;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"[WorkspaceAuditSubscriber] Audit write failed: {ex.Message}");
            }
        }
    }
}
