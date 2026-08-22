using System.Net;
using System.Net.Http.Json;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
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
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Storage:DataDirectory", _dataDirectory));
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    [Test]
    public async Task FrontendEvents_AreReturnedOnlyForRequestedApplication()
    {
        using var client = _factory.CreateClient();
        await RecordAsync(client, "web", "load_failed");
        await RecordAsync(client, "api", "request_complete");

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

    private static async Task RecordAsync(HttpClient client, string application, string action)
    {
        using var response = await client.PostAsJsonAsync("/api/audit/record", new AuditRecordRequest(
            "frontend", "web", action, "failure", "ws-1",
            new Dictionary<string, string> { ["application"] = application }));
        response.EnsureSuccessStatusCode();
    }
}
