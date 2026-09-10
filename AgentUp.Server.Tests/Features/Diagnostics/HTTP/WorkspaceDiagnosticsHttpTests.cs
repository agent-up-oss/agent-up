using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Diagnostics.HTTP;

[TestFixture]
public sealed class WorkspaceDiagnosticsHttpTests
{
    private string _dataDirectory = null!;
    private WebApplicationFactory<Program> _parentFactory = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-diagnostics-http-{Guid.NewGuid():N}");
        _parentFactory = new WebApplicationFactory<Program>();
        _factory = _parentFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataDirectory", _dataDirectory);
            builder.UseSetting("AGENTUP_AUTH_DISABLED", "true");
        });
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        _parentFactory.Dispose();
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    [Test]
    public async Task Get_ReturnsOnlyRequestedWorkspaceAndApplicationDiagnostics()
    {
        using var client = _factory.CreateClient();
        using var registrationResponse = await client.PostAsJsonAsync("/api/workspaces", new RegisterWorkspaceRequest(
            "Shop", "/repo", "/repo", "main", "abc")
        {
            Applications = [new ApplicationDefinition("web", "npm start", ".", [])]
        });
        registrationResponse.EnsureSuccessStatusCode();
        using var registration = await JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
        var workspaceId = registration.RootElement.GetProperty("id").GetString()!;
        await RecordAsync(client, workspaceId, "web", "javascript_exception");
        await RecordAsync(client, workspaceId, "api", "network_request_failed");
        await RecordAsync(client, "different-workspace", "web", "browser_error");

        var result = await client.GetFromJsonAsync<WorkspaceDiagnosticsDto>(
            $"/api/diagnostics/workspaces/{workspaceId}?application=web");

        Assert.Multiple(() =>
        {
            Assert.That(result!.WorkspaceId, Is.EqualTo(workspaceId));
            Assert.That(result.Applications.Select(app => app.Name), Is.EqualTo(["web"]));
            Assert.That(result.Entries.Select(entry => entry.Action), Is.EqualTo(["javascript_exception"]));
            Assert.That(result.Entries.Single().State, Is.EqualTo("active"));
        });
    }

    [Test]
    public async Task Get_ReturnsNotFoundForUnknownWorkspace()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/diagnostics/workspaces/missing");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Get_ReturnsBadRequestForOutOfRangeLimits()
    {
        using var client = _factory.CreateClient();
        using var registrationResponse = await client.PostAsJsonAsync("/api/workspaces", new RegisterWorkspaceRequest(
            "Shop", "/repo", "/repo", "main", "abc")
        {
            Applications = [new ApplicationDefinition("web", "npm start", ".", [])]
        });
        registrationResponse.EnsureSuccessStatusCode();
        using var registration = await JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
        var workspaceId = registration.RootElement.GetProperty("id").GetString()!;

        using var invalidLogLimit = await client.GetAsync(
            $"/api/diagnostics/workspaces/{workspaceId}?logLimit=0");
        using var invalidEntryLimit = await client.GetAsync(
            $"/api/diagnostics/workspaces/{workspaceId}?entryLimit=501");

        Assert.Multiple(() =>
        {
            Assert.That(invalidLogLimit.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(invalidEntryLimit.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    private static async Task RecordAsync(HttpClient client, string workspaceId, string application, string action)
    {
        using var response = await client.PostAsJsonAsync("/api/audit/record", new AuditRecordRequest(
            "frontend", "web", action, "failure", workspaceId,
            new Dictionary<string, string> { ["application"] = application, ["message"] = action }));
        response.EnsureSuccessStatusCode();
    }
}
