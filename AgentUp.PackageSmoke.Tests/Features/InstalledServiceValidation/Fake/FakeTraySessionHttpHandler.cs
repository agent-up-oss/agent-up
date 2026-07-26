using System.Net;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Fake;

/// <summary>
/// Returns successful HTTP responses for tray session endpoints so smoke validator tests
/// don't need a real server running. Records calls and rejects non-POST requests.
/// </summary>
internal sealed class FakeTraySessionHttpHandler : HttpMessageHandler
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));

        var path = request.RequestUri?.AbsolutePath;
        if (path is not null)
            _calls.Add(path);

        var status = path switch
        {
            "/api/tray/heartbeat" => HttpStatusCode.OK,
            "/api/service/restart" => HttpStatusCode.Accepted,
            "/api/service/shutdown" => HttpStatusCode.Accepted,
            _ => HttpStatusCode.NotFound
        };
        return Task.FromResult(new HttpResponseMessage(status));
    }
}
