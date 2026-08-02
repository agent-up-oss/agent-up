using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Processes.Unit;

[TestFixture]
public sealed class ProcessOutputServiceTests
{
    [Test]
    public async Task AppendAsync_StoresOutputAndRecordsConsoleAuditEvent()
    {
        var output = new InMemoryOutputRepository();
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(events: events);
        var service = new ProcessOutputService(
            output,
            audit,
            NullLogger<ProcessOutputService>.Instance);

        await service.AppendAsync("workspace", "Web", "[err] failed", ProcessOutputStream.Stderr);

        var lines = await output.GetAsync("workspace", "Web");
        Assert.That(lines, Is.EqualTo(new[] { "[err] failed" }));
        var evt = events.Events.Single();
        Assert.That(evt.Kind, Is.EqualTo("application"));
        Assert.That(evt.Source, Is.EqualTo("process"));
        Assert.That(evt.Action, Is.EqualTo("application_console_line"));
        Assert.That(evt.WorkspaceId, Is.EqualTo("workspace"));
        Assert.That(evt.Details["applicationName"], Is.EqualTo("Web"));
        Assert.That(evt.Details["stream"], Is.EqualTo("stderr"));
        Assert.That(evt.Details["message"], Is.EqualTo("failed"));
    }

    [Test]
    public async Task AppendAsync_PreservesStdoutLineThatStartsWithErrPrefix()
    {
        var output = new InMemoryOutputRepository();
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(events: events);
        var service = new ProcessOutputService(
            output,
            audit,
            NullLogger<ProcessOutputService>.Instance);

        await service.AppendAsync("workspace", "Web", "[err] this is stdout");

        var lines = await output.GetAsync("workspace", "Web");
        Assert.That(lines, Is.EqualTo(new[] { "[err] this is stdout" }));
        var evt = events.Events.Single();
        Assert.That(evt.Details["stream"], Is.EqualTo("stdout"));
        Assert.That(evt.Details["message"], Is.EqualTo("[err] this is stdout"));
    }

    [Test]
    public async Task AppendAsync_RedactsConsoleAuditMessage()
    {
        var output = new InMemoryOutputRepository();
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(events: events);
        var service = new ProcessOutputService(
            output,
            audit,
            NullLogger<ProcessOutputService>.Instance);

        await service.AppendAsync("workspace", "Web", "token=abc123 password:secret");

        var evt = events.Events.Single();
        Assert.That(evt.Details["message"], Is.EqualTo("token=[REDACTED] password:[REDACTED]"));
    }

    [Test]
    public async Task AppendAsync_KeepsOutput_WhenAuditRecordingFails()
    {
        var output = new InMemoryOutputRepository();
        var audit = ServerTestComposition.CreateAuditController(events: new FailingAuditEventRepository());
        var service = new ProcessOutputService(
            output,
            audit,
            NullLogger<ProcessOutputService>.Instance);

        await service.AppendAsync("workspace", "Web", "ready");

        var lines = await output.GetAsync("workspace", "Web");
        Assert.That(lines, Is.EqualTo(new[] { "ready" }));
    }

    private sealed class FailingAuditEventRepository : IAuditEventRepository
    {
        public Task AppendAsync(AuditEvent evt, CancellationToken cancellationToken)
            => throw new InvalidOperationException("audit unavailable");

        public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AuditEvent>>([]);

        public Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken)
            => Task.FromResult<AuditEvent?>(null);
    }
}
