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
        var client = new WorkspaceApiClient(new HttpClient(new UnauthorizedHandler())
        {
            BaseAddress = new Uri("http://localhost")
        });

        var exception = Assert.ThrowsAsync<AuthenticationRequiredException>(client.ListAsync);
        Assert.That(exception!.Message, Is.EqualTo(AuthenticationRequiredException.LoginHint));
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }
}
