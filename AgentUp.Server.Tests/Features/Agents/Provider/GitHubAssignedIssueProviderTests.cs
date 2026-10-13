using System.Net;
using System.Net.Http;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class GitHubAssignedIssueProviderTests
{
    [Test]
    public async Task ListAssignedAsync_ordersOpenAssignedIssuesByNumber()
    {
        var http = new HttpClient(new FixedHandler("""
            [
              {"id": 20, "number": 2, "title": "Two", "body": "b", "created_at": "2020-01-02T00:00:00Z", "updated_at": "2020-01-02T00:00:00Z", "html_url": "https://example/2"},
              {"id": 10, "number": 1, "title": "One", "body": "a", "created_at": "2020-01-01T00:00:00Z", "updated_at": "2020-01-03T00:00:00Z", "html_url": "https://example/1"}
            ]
            """));
        var provider = new GitHubAssignedIssueProvider(http);

        var items = await provider.ListAssignedAsync("ws", AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(item => item.Number), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(items[0].Title, Is.EqualTo("One"));
        });
    }

    [Test]
    public void ListAssignedAsync_throwsWhenTheHostIsUnavailable()
    {
        var http = new HttpClient(new FixedHandler("nope", HttpStatusCode.ServiceUnavailable));
        var provider = new GitHubAssignedIssueProvider(http);

        Assert.That(
            async () => await provider.ListAssignedAsync("ws", AssignedIssueOrder.NumberAsc, CancellationToken.None),
            Throws.TypeOf<HttpRequestException>());
    }

    private sealed class FixedHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
    }
}
