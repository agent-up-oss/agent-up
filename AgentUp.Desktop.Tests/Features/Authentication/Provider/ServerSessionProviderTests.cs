using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class ServerSessionProviderTests
{
    [Test]
    public void Apply_setsBaseAddressAndBearerToken()
    {
        using var http = new HttpClient();

        ServerSessionProvider.Apply(http, new Uri("http://127.0.0.1:5100/"), "token-1");

        Assert.Multiple(() =>
        {
            Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://127.0.0.1:5100/")));
            Assert.That(http.DefaultRequestHeaders.Authorization, Is.EqualTo(new AuthenticationHeaderValue("Bearer", "token-1")));
        });
    }

    [Test]
    public void Apply_clearsAuthorizationWhenTokenIsMissing()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "old");

        ServerSessionProvider.Apply(http, new Uri("https://agent-up.example.com"), null);

        Assert.That(http.DefaultRequestHeaders.Authorization, Is.Null);
    }

    [Test]
    public async Task Apply_afterARequest_sendsTheNextCallToTheNewServer()
    {
        Uri? lastUri = null;
        using var http = new DisposableTestHttpClient(request =>
        {
            lastUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        _ = await http.Client.GetAsync("/api/auth/status");
        Assert.That(lastUri!.Port, Is.EqualTo(5000));

        ServerSessionProvider.Apply(http.Client, new Uri("http://127.0.0.1:5100/"), "token-1");
        _ = await http.Client.GetAsync("/api/auth/status");

        Assert.Multiple(() =>
        {
            Assert.That(lastUri!.Port, Is.EqualTo(5100));
            Assert.That(http.Client.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("token-1"));
        });
    }
}
