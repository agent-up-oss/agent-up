using System.Net.Http.Json;
using AgentUp.Desktop.Features.Workspaces.Http;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class FakeHttpMessageHandler(List<WorkspaceDto> workspaces) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var content = JsonContent.Create(workspaces);
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
    }
}

internal sealed class ErrorHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
}
