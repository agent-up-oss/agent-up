using System.Net;
using System.Diagnostics.CodeAnalysis;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceOverviewApiClientTests
{
    [Test]
    public async Task GetOverviewAsync_readsTheWorkspaceOverviewRoute()
    {
        using var handler = new OverviewHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        var overview = await client.GetOverviewAsync("ws-1");

        Assert.That(handler.LastPath, Is.EqualTo("/api/workspaces/ws-1/overview"));
        Assert.That(overview!.DisplayName, Is.EqualTo("Agent Up"));
        Assert.That(overview.CpuPercent, Is.EqualTo(4.2));
        Assert.That(overview.StorageBytes, Is.EqualTo(4096));
    }

    private sealed class OverviewHandler : HttpMessageHandler
    {
        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.AbsolutePath;
            return Task.FromResult(Respond(
                HttpStatusCode.OK,
                """{"id":"ws-1","displayName":"Agent Up","repositoryPath":"/repo","worktreePath":"/worktree","branch":"main","commit":"abc","state":"Running","cpuPercent":4.2,"memoryBytes":1024,"storageBytes":4096,"processCount":1,"applicationCount":2}"""));
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
        private static HttpResponseMessage Respond(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }
}
