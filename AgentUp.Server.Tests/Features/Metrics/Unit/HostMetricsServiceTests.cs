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

    [Test]
    public async Task StopAsync_completes_cleanly_after_immediate_cancellation()
    {
        var service = new HostMetricsService(
            ServerTestComposition.CreateAuditController(events: new InMemoryAuditEventRepository()),
            NullLogger<HostMetricsService>.Instance);
        using var cancellation = new CancellationTokenSource();

        await service.StartAsync(cancellation.Token);
        cancellation.Cancel();

        Assert.DoesNotThrowAsync(async () => await service.StopAsync(CancellationToken.None));
    }
}
