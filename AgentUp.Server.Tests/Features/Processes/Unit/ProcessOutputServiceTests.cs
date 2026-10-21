using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
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
        var audit = ServerTestComposition.CreateAuditController(ServerTestComposition.CreateRegistry(), events);
        var service = new ProcessOutputService(
            output,
            audit,
            NullLogger<ProcessOutputService>.Instance);

        await service.AppendAsync("workspace", "Web", "[err] failed", ProcessOutputStream.Stderr);

        var lines = await output.GetAsync("workspace", "Web");
        Assert.That(lines.Texts(), Is.EqualTo(new[] { "[err] failed" }));
        Assert.That(lines.Single().Stream, Is.EqualTo(ProcessOutputStream.Stderr));
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
        var audit = ServerTestComposition.CreateAuditController(ServerTestComposition.CreateRegistry(), events);
        var service = new ProcessOutputService(
            output,
            audit,
            NullLogger<ProcessOutputService>.Instance);

        await service.AppendAsync("workspace", "Web", "[err] this is stdout");

        var lines = await output.GetAsync("workspace", "Web");
        Assert.That(lines.Texts(), Is.EqualTo(new[] { "[err] this is stdout" }));
        Assert.That(lines.Single().Stream, Is.EqualTo(ProcessOutputStream.Stdout));
        var evt = events.Events.Single();
        Assert.That(evt.Details["stream"], Is.EqualTo("stdout"));
        Assert.That(evt.Details["message"], Is.EqualTo("[err] this is stdout"));
    }

    [Test]
    public async Task AppendAsync_RedactsConsoleAuditMessage()
    {
        var output = new InMemoryOutputRepository();
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(ServerTestComposition.CreateRegistry(), events);
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
        var audit = ServerTestComposition.CreateAuditController(ServerTestComposition.CreateRegistry(), new FailingAuditEventRepository());
        var service = new ProcessOutputService(
            output,
            audit,
            NullLogger<ProcessOutputService>.Instance);

        await service.AppendAsync("workspace", "Web", "ready");

        var lines = await output.GetAsync("workspace", "Web");
        Assert.That(lines.Texts(), Is.EqualTo(new[] { "ready" }));
    }

    [Test]
    public async Task AppendAsync_serialisesConcurrentStdoutAndStderrWithoutSplicing()
    {
        var output = new InMemoryOutputRepository();
        var service = new ProcessOutputService(output);
        var tasks = Enumerable.Range(0, 100).SelectMany(index => new[]
        {
            service.AppendAsync("workspace", "Web", $"out-{index:000}"),
            service.AppendAsync("workspace", "Web", $"[err] err-{index:000}", ProcessOutputStream.Stderr)
        });

        await Task.WhenAll(tasks);

        var lines = await output.GetAsync("workspace", "Web");
        Assert.That(lines, Has.Count.EqualTo(200));
        Assert.That(lines.Count(line => line.Stream == ProcessOutputStream.Stdout), Is.EqualTo(100));
        Assert.That(lines.Select(line => line.Text), Has.All.Match(@"^(out-\d{3}|\[err\] err-\d{3})$"));
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
