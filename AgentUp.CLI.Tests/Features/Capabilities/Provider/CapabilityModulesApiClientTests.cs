using System.Net;
using System.Text.Json;
using AgentUp.CLI.Features.Capabilities.Providers;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityModulesApiClientTests
{
    [Test]
    public async Task ListAsync_returnsModulesFromTheServer()
    {
        var module = CliDomain.Capability().Enabled().Build();
        using var handler = new StubHandler(_ => Json(module, wrapInArray: true));
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var listed = await new CapabilityModulesApiClient(http).ListAsync();

        Assert.That(listed.Single().Id, Is.EqualTo(CliDomain.CapabilityId));
    }

    [Test]
    public async Task EnableAsync_postsThePackageIdentity()
    {
        HttpRequestMessage? posted = null;
        using var handler = new StubHandler(request =>
        {
            posted = request;
            return Json(CliDomain.Capability().Enabled().Build());
        });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var enabled = await new CapabilityModulesApiClient(http).EnableAsync(CliDomain.CapabilityId, CliDomain.CapabilityVersion);

        Assert.Multiple(() =>
        {
            Assert.That(enabled.Enabled, Is.True);
            Assert.That(posted!.RequestUri!.AbsolutePath, Is.EqualTo("/api/capabilities/enable"));
        });
    }

    [Test]
    public async Task DisableAsync_postsThePackageId()
    {
        HttpRequestMessage? posted = null;
        using var handler = new StubHandler(request =>
        {
            posted = request;
            return Json(CliDomain.Capability().Build());
        });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var disabled = await new CapabilityModulesApiClient(http).DisableAsync(CliDomain.CapabilityId);

        Assert.Multiple(() =>
        {
            Assert.That(disabled.Enabled, Is.False);
            Assert.That(posted!.RequestUri!.AbsolutePath, Is.EqualTo($"/api/capabilities/disable/{CliDomain.CapabilityId}"));
        });
    }

    private static HttpResponseMessage Json(object payload, bool wrapInArray = false)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                wrapInArray ? JsonSerializer.Serialize(new[] { payload }) : JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json")
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
