using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Workspaces.HTTP;

[TestFixture]
public sealed class LoopbackClientCorsHttpTests
{
    private string _dataDirectory = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-cors-{Guid.NewGuid():N}");
        using var factory = new WebApplicationFactory<Program>();
        _factory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Storage:DataDirectory", _dataDirectory));
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    [TestCase("http://localhost:8081")]
    [TestCase("https://localhost:8443")]
    [TestCase("http://127.0.0.1:8081")]
    public async Task Workspaces_AllowsLoopbackWebClientOrigins(string origin)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspaces");
        request.Headers.Add("Origin", origin);

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo([origin]));
    }

    [Test]
    public async Task Workspaces_DoesNotAllowNonLoopbackWebClientOrigins()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspaces");
        request.Headers.Add("Origin", "https://untrusted.example");

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }
}
