using System.Collections.Concurrent;
using System.Net;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Tests.Features.Authentication.Provider;

[TestFixture]
public class WorkspaceApiClientUnauthorizedTests
{
    [Test]
    public void ListAsync_throwsAuthenticationRequired_onUnauthorized()
    {
        using var handler = new UnauthorizedHandler();
        using var http = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var client = new WorkspaceApiClient(http);

        var exception = Assert.ThrowsAsync<AuthenticationRequiredException>(client.ListAsync);
        Assert.That(exception!.Message, Is.EqualTo(AuthenticationRequiredException.LoginHint));
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        private readonly ConcurrentBag<HttpResponseMessage> _responses = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            _responses.Add(response);
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var response in _responses)
                    response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
