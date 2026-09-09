namespace AgentUp.Desktop.Tests.Support;

internal sealed class DisposableTestHttpClient : IDisposable
{
    private readonly StubHandler _handler;
    private readonly HttpClient _client;

    public DisposableTestHttpClient(Func<HttpRequestMessage, HttpResponseMessage> response, string baseAddress = "http://127.0.0.1:5000")
    {
        _handler = new StubHandler(response);
        _client = new HttpClient(_handler) { BaseAddress = new Uri(baseAddress) };
    }

    public HttpClient Client => _client;

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
