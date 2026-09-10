using System.Net;
using System.Net.Http.Json;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Audit.HTTP;

[TestFixture]
public sealed class ApplicationAuditHttpTests
{
    private string _dataDirectory = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-audit-http-{Guid.NewGuid():N}");
        using var factory = new WebApplicationFactory<Program>();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataDirectory", _dataDirectory);
            builder.UseSetting("AGENTUP_AUTH_DISABLED", "true");
        });
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    [Test]
    public async Task ApplicationQuery_FiltersByStreams()
    {
        using var client = _factory.CreateClient();
        await RecordConsoleAsync(client, "web", "stdout", "ready on port 8080");
        await RecordConsoleAsync(client, "web", "stderr", "1/1 brokers are down");

        var page = await client.GetFromJsonAsync<AuditEventPageDto>(
            "/api/audit/workspaces/ws-1/applications/web?kinds=application&streams=stderr&limit=50");

        Assert.That(page!.Items.Select(item => item.Details["stream"]), Is.EqualTo(["stderr"]));
    }

    [Test]
    public async Task ApplicationQuery_FiltersByKinds()
    {
        using var client = _factory.CreateClient();
        await RecordAsync(client, "web", "frontend", "load_failed");
        await RecordAsync(client, "web", "application", "application_console_line");
        await RecordAsync(client, "web", "health", "port_health_check");

        var page = await client.GetFromJsonAsync<AuditEventPageDto>(
            "/api/audit/workspaces/ws-1/applications/web?kinds=health&kinds=frontend&limit=50");

        Assert.That(page!.Items.Select(item => item.Kind), Is.EquivalentTo(["frontend", "health"]));
    }

    [Test]
    public async Task FrontendEvents_AreReturnedOnlyForRequestedApplication()
    {
        using var client = _factory.CreateClient();
        await RecordAsync(client, "web", "frontend", "load_failed");
        await RecordAsync(client, "api", "frontend", "request_complete");

        var page = await client.GetFromJsonAsync<AuditEventPageDto>(
            "/api/audit/workspaces/ws-1/applications/web?limit=50");

        Assert.That(page!.Items.Select(item => item.Action), Is.EqualTo(["load_failed"]));
        Assert.That(page.NextBefore, Is.Null);
    }

    [Test]
    public async Task ApplicationQuery_RejectsUnboundedPageSize()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/audit/workspaces/ws-1/applications/web?limit=101");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ApplicationQuery_CompositeCursorDoesNotSkipEqualTimestamps()
    {
        var timestamp = DateTimeOffset.Parse("2026-08-22T12:00:00Z");
        var repository = new FileAuditEventRepository(_dataDirectory);
        await repository.AppendAsync(CreateEvent("event-b", timestamp), CancellationToken.None);
        await repository.AppendAsync(CreateEvent("event-a", timestamp), CancellationToken.None);
        using var client = _factory.CreateClient();

        var first = await client.GetFromJsonAsync<AuditEventPageDto>(
            "/api/audit/workspaces/ws-1/applications/web?limit=1");
        var second = await client.GetFromJsonAsync<AuditEventPageDto>(
            $"/api/audit/workspaces/ws-1/applications/web?limit=1&before={Uri.EscapeDataString(first!.NextBefore!.Value.ToString("O"))}&beforeEventId={first.NextBeforeEventId}");

        Assert.Multiple(() =>
        {
            Assert.That(first.Items.Single().EventId, Is.EqualTo("event-b"));
            Assert.That(second!.Items.Single().EventId, Is.EqualTo("event-a"));
        });
    }

    private static AuditEvent CreateEvent(string eventId, DateTimeOffset timestamp)
        => new(eventId, timestamp, "frontend", "web", "load", "success", "ws-1",
            null, null, null, null, null, null,
            new Dictionary<string, string> { ["application"] = "web" }, []);

    private static async Task RecordConsoleAsync(HttpClient client, string application, string stream, string message)
    {
        using var response = await client.PostAsJsonAsync("/api/audit/record", new AuditRecordRequest(
            "application", "process", "application_console_line", "success", "ws-1",
            new Dictionary<string, string>
            {
                ["application"] = application,
                ["applicationName"] = application,
                ["stream"] = stream,
                ["message"] = message
            }));
        response.EnsureSuccessStatusCode();
    }

    private static async Task RecordAsync(HttpClient client, string application, string kind, string action)
    {
        using var response = await client.PostAsJsonAsync("/api/audit/record", new AuditRecordRequest(
            kind, kind == "health" ? "server" : kind == "application" ? "process" : "web", action, "failure", "ws-1",
            new Dictionary<string, string> { ["application"] = application, ["applicationName"] = application }));
        response.EnsureSuccessStatusCode();
    }
}
