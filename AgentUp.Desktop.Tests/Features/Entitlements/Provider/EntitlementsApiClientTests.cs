using System.Net;
using AgentUp.Desktop.Features.Entitlements.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Entitlements.Provider;

[TestFixture]
public sealed class EntitlementsApiClientTests
{
    [Test]
    public async Task GetAsync_readsThePermissionDocument()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Json(new
        {
            displayName = "Community",
            billing = "free",
            features = new Dictionary<string, object> { ["agent.prompt"] = new { available = true } },
            limits = new Dictionary<string, object>()
        }));
        var client = new EntitlementsApiClient(http.Client);

        var document = await client.GetAsync();

        Assert.That(document!.DisplayName, Is.EqualTo("Community"));
    }

    [Test]
    public async Task GetAsync_returnsNullWhenUnauthorized()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Empty(HttpStatusCode.Unauthorized));
        var client = new EntitlementsApiClient(http.Client);

        Assert.That(await client.GetAsync(), Is.Null);
    }

    [Test]
    public async Task GetAsync_returnsNullWhenThePayloadIsInvalidJson()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Text(HttpStatusCode.OK, "{not-json"));
        var client = new EntitlementsApiClient(http.Client);

        Assert.That(await client.GetAsync(), Is.Null);
    }

    [Test]
    public async Task GetAsync_returnsNullWhenTheRequestTimesOut()
    {
        using var http = new HttpClient(new CancelledHandler()) { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var client = new EntitlementsApiClient(http);

        Assert.That(await client.GetAsync(), Is.Null);
    }

    [Test]
    public async Task GetAsync_throwsWhenTheCallerCancels()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Json(new { displayName = "Community" }));
        var client = new EntitlementsApiClient(http.Client);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await client.GetAsync(cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    private sealed class CancelledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(new TaskCanceledException());
    }
}
