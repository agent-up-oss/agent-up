using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class DisposableTestHttpClient : IDisposable
{
    private readonly HttpClient _client;

    public DisposableTestHttpClient(Func<HttpRequestMessage, HttpResponseMessage> response, string baseAddress = "http://127.0.0.1:5000")
    {
        _client = ServerSessionProvider.CreateClient(new Uri(baseAddress), new StubHandler(response));
    }

    public HttpClient Client => _client;

    public void Dispose() => _client.Dispose();

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
