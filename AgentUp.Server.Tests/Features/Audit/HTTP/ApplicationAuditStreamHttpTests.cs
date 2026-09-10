using System.Net.Http.Json;
using AgentUp.Server.Features.Audit.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Audit.HTTP;

[TestFixture]
public sealed class ApplicationAuditStreamHttpTests
{
    private string _dataDirectory = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-audit-stream-http-{Guid.NewGuid():N}");
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
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }

    [Test]
    public async Task StreamEndpoint_WritesMatchingEventsAsServerSentEvents()
    {
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var readTask = Task.Run(async () =>
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/audit/workspaces/ws-1/applications/web/stream?kinds=frontend");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);
            return await reader.ReadLineAsync(cts.Token);
        }, cts.Token);

        await Task.Delay(200, cts.Token);
        await RecordAsync(client, "web", "frontend", "load_failed");
        await RecordAsync(client, "web", "health", "port_health_check");

        var firstLine = await readTask;

        Assert.Multiple(() =>
        {
            Assert.That(firstLine, Does.StartWith("data: "));
            Assert.That(firstLine, Does.Contain("load_failed"));
            Assert.That(firstLine, Does.Not.Contain("port_health_check"));
        });
    }

    private static async Task RecordAsync(HttpClient client, string application, string kind, string action)
    {
        using var response = await client.PostAsJsonAsync("/api/audit/record", new AuditRecordRequest(
            kind,
            kind == "health" ? "server" : "web",
            action,
            "failure",
            "ws-1",
            new Dictionary<string, string> { ["application"] = application, ["applicationName"] = application }));
        response.EnsureSuccessStatusCode();
    }
}
