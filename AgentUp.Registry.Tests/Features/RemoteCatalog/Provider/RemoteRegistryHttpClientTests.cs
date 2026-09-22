using System.Net;
using System.Net.Http.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.RemoteCatalog.DTOs;
using AgentUp.Registry.Features.RemoteCatalog.Providers;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.RemoteCatalog.Provider;

[TestFixture]
public sealed class RemoteRegistryHttpClientTests
{
    [Test]
    public async Task ListAsync_reads_the_catalog_payload()
    {
        using var handler = new StubHandler(
            HttpStatusCode.OK,
            new RemoteCatalogListDto([
                new CapabilityRegistryIndexEntry(
                    RegistryDomain.DotnetId,
                    RegistryDomain.DotnetVersion,
                    RegistryDomain.DotnetDisplayName,
                    RegistryDomain.Publisher,
                    "runtime")
            ]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://registry.test/") };
        var client = new RemoteRegistryHttpClient(http);

        var packages = await client.ListAsync(CancellationToken.None);

        Assert.That(packages.Single().Id, Is.EqualTo(RegistryDomain.DotnetId));
    }

    [Test]
    public async Task DownloadAsync_returns_package_bytes()
    {
        using var handler = new StubHandler(HttpStatusCode.OK, payload: null, bytes: [1, 2, 3]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://registry.test/") };
        var client = new RemoteRegistryHttpClient(http);

        var package = await client.DownloadAsync(RegistryDomain.DotnetId, RegistryDomain.DotnetVersion, CancellationToken.None);

        Assert.That(package.Archive, Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(package.Id, Is.EqualTo(RegistryDomain.DotnetId));
    }

    [Test]
    public async Task PushAsync_sends_the_archive_with_a_bearer_token()
    {
        using var handler = new StubHandler(HttpStatusCode.NoContent);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://registry.test/") };
        var client = new RemoteRegistryHttpClient(http);

        await client.PushAsync(
            new RemotePackageBytesDto(RegistryDomain.DotnetId, RegistryDomain.DotnetVersion, [9, 8, 7]),
            "secret-token",
            CancellationToken.None);

        Assert.That(handler.LastMethod, Is.EqualTo(HttpMethod.Put));
        Assert.That(handler.LastAuthorization, Is.EqualTo("Bearer secret-token"));
    }

    private sealed class StubHandler(HttpStatusCode status, object? payload = null, byte[]? bytes = null) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastAuthorization = request.Headers.Authorization?.ToString();
            var response = new HttpResponseMessage(status);
            if (bytes is not null)
                response.Content = new ByteArrayContent(bytes);
            else if (payload is not null)
                response.Content = JsonContent.Create(payload);
            return Task.FromResult(response);
        }
    }
}
