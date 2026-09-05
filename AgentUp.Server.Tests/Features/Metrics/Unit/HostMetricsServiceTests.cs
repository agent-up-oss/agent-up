using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Metrics.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Metrics.Unit;

[TestFixture]
public sealed class HostMetricsServiceTests
{
    [Test]
    public async Task ExecuteAsync_RecordsHostServerMetrics()
    {
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(events: events);
        var service = new HostMetricsService(audit, NullLogger<HostMetricsService>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await service.StartAsync(cts.Token);
        await Task.Delay(1200, cts.Token);
        await service.StopAsync(CancellationToken.None);

        Assert.That(events.Events.Any(evt =>
            evt.Kind == "metrics"
            && evt.Scope == AuditScope.HostServer
            && evt.Details.ContainsKey("process.workingSetBytes")), Is.True);
    }
}
