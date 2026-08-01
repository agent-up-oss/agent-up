using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceAuditSubscriber(
    WorkspaceEventBus workspaceEvents,
    AuditController audit) : BackgroundService
{
    private readonly Dictionary<string, string> _lastFingerprints = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sub = workspaceEvents.Subscribe();
        await foreach (var evt in sub.Reader.ReadAllAsync(stoppingToken))
        {
            var applications = string.Join(',', evt.Applications.Select(app => $"{app.Name}:{app.State}"));
            var fingerprint = $"{evt.State}|{applications}";
            if (_lastFingerprints.TryGetValue(evt.WorkspaceId, out var previous)
                && string.Equals(previous, fingerprint, StringComparison.Ordinal))
                continue;
            _lastFingerprints[evt.WorkspaceId] = fingerprint;

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
        }
    }
}
