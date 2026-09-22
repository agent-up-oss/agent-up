using System.Net;
using System.Diagnostics.CodeAnalysis;
using AgentUp.Desktop.Features.Capabilities.Providers;

namespace AgentUp.Desktop.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityModulesApiClientTests
{
    [Test]
    public async Task ListAsync_readsTheServerCatalog()
    {
        using var handler = new RecordingCapabilityHandler(HttpStatusCode.OK,
            """[{"id":"dotnet","version":"1.0.0","displayName":".NET","publisher":"agent-up","kind":"runtime","enabled":true,"state":"ready","canRun":true,"messages":[]}]""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new CapabilityModulesApiClient(http);

        var modules = await client.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(modules.Single().Id, Is.EqualTo("dotnet"));
            Assert.That(modules.Single().CanRun, Is.True);
            Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/capabilities"));
        });
    }

    [Test]
    public async Task EnableAsync_postsThePackageIdAndVersion()
    {
        using var handler = new RecordingCapabilityHandler(HttpStatusCode.OK,
            """{"id":"docker","version":"1.0.0","displayName":"Docker","publisher":"agent-up","kind":"runtime","enabled":true,"state":"ready","canRun":true,"messages":[]}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new CapabilityModulesApiClient(http);

        var module = await client.EnableAsync("docker", "1.0.0");

        Assert.Multiple(() =>
        {
            Assert.That(module.Enabled, Is.True);
            Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/capabilities/enable"));
            Assert.That(handler.LastRequestBody, Does.Contain("\"id\":\"docker\""));
            Assert.That(handler.LastRequestBody, Does.Contain("\"version\":\"1.0.0\""));
        });
    }

    [Test]
    public async Task DisableAsync_postsTheEscapedPackageId()
    {
        using var handler = new RecordingCapabilityHandler(HttpStatusCode.OK,
            """{"id":"dotnet","version":"1.0.0","displayName":".NET","publisher":"agent-up","kind":"runtime","enabled":false,"state":"disabled","canRun":false,"messages":["not enabled"]}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new CapabilityModulesApiClient(http);

        var module = await client.DisableAsync("dot net");

        Assert.Multiple(() =>
        {
            Assert.That(module.Enabled, Is.False);
            Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/capabilities/disable/dot%20net"));
        });
    }
}

internal sealed class RecordingCapabilityHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public Uri? LastUri { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastUri = request.RequestUri;
        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(ct);

        return Respond(status, body);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage Respond(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}
