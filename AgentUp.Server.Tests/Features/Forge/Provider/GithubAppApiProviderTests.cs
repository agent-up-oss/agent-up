using System.Net;
using System.Security.Cryptography;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;

namespace AgentUp.Server.Tests.Features.Forge.Provider;

[TestFixture]
public sealed class GithubAppApiProviderTests
{
    [Test]
    public async Task ConvertManifestAsync_recordsTheGeneratedAppIdentity()
    {
        var handler = new RecordedHandler("""{"id":42,"slug":"agent-up-main","pem":"secret"}""");
        var provider = new GithubAppApiProvider(handler);

        var result = await provider.ConvertManifestAsync(Connection(), "code/value", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests.Single().Uri, Is.EqualTo("https://api.github.test/app-manifests/code%2Fvalue/conversions"));
            Assert.That(result.AppId, Is.EqualTo(42));
            Assert.That(result.Slug, Is.EqualTo("agent-up-main"));
            Assert.That(result.PrivateKey, Is.EqualTo("secret"));
        });
    }

    [Test]
    public async Task CreateInstallationTokenAsync_usesAnAppJwtAndRestrictsTheRepository()
    {
        using var rsa = RSA.Create(2048);
        var connection = Connection() with
        {
            AppId = 42,
            PrivateKey = rsa.ExportRSAPrivateKeyPem(),
            InstallationId = 99
        };
        var handler = new RecordedHandler("""{"token":"installation-token"}""");
        var provider = new GithubAppApiProvider(handler);

        var token = await provider.CreateInstallationTokenAsync(connection, "widgets", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.EqualTo("installation-token"));
            Assert.That(handler.Requests.Single().Uri, Is.EqualTo("https://api.github.test/app/installations/99/access_tokens"));
            Assert.That(handler.Requests.Single().Authorization, Does.StartWith("Bearer ey"));
            Assert.That(handler.Requests.Single().Body, Does.Contain("\"repositories\":[\"widgets\"]"));
        });
    }

    [Test]
    public async Task VerifyInstallationAsync_requiresTheInferredRepositoryOwner()
    {
        using var rsa = RSA.Create(2048);
        var connection = Connection() with
        {
            AppId = 42,
            PrivateKey = rsa.ExportRSAPrivateKeyPem(),
            RepositoryOwner = "acme"
        };
        var provider = new GithubAppApiProvider(
            new RecordedHandler("""{"account":{"login":"someone-else"}}"""));

        var verified = await provider.VerifyInstallationAsync(connection, 99, CancellationToken.None);

        Assert.That(verified, Is.False);
    }

    private static GithubAppConnection Connection()
        => new(
            "workspace-1",
            "state",
            "setup",
            "Agent-Up-Main-12345678",
            "http://localhost:5000",
            "https://api.github.test/",
            "https://github.test");

    private sealed class RecordedHandler(string response) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RecordedRequest(string Uri, string? Authorization, string Body);
}
