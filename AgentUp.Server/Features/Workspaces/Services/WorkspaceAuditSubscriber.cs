using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceAuditSubscriber(
    WorkspaceEventBus workspaceEvents,
    AuditController audit) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sub = workspaceEvents.Subscribe();
        await foreach (var evt in sub.Reader.ReadAllAsync(stoppingToken))
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
                        ["applications"] = string.Join(',', evt.Applications.Select(app => $"{app.Name}:{app.State}"))
                    }),
                stoppingToken);
        }
    }
}
