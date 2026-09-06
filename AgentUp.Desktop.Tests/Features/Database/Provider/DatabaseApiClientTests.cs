using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Database.Providers;

namespace AgentUp.Desktop.Tests.Features.Database.Provider;

[TestFixture]
public sealed class DatabaseApiClientTests
{
    [Test]
    public async Task GetCatalog_escapesWorkspaceAndApplicationNames()
    {
        var handler = new RecordingHandler();
        var client = new DatabaseApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        await client.GetCatalogAsync("work space", "data/base", CancellationToken.None);
        Assert.That(handler.Path, Is.EqualTo("/api/workspaces/work%20space/applications/data%2Fbase/database"));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Path { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"databases\":[]}", Encoding.UTF8, "application/json") });
        }
    }
}
