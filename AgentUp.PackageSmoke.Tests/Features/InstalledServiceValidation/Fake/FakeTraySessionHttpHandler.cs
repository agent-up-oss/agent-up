using System.Net;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Fake;

/// <summary>
/// Returns successful HTTP responses for tray session endpoints so smoke validator tests
/// don't need a real server running.
/// </summary>
internal sealed class FakeTraySessionHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var status = request.RequestUri?.AbsolutePath switch
        {
            "/api/tray/heartbeat" => HttpStatusCode.OK,
            "/api/service/restart" => HttpStatusCode.Accepted,
            "/api/service/shutdown" => HttpStatusCode.Accepted,
            _ => HttpStatusCode.NotFound
        };
        return Task.FromResult(new HttpResponseMessage(status));
    }
}
