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

internal sealed class MutableFakeHttpMessageHandler(List<WorkspaceDto> initial) : HttpMessageHandler
{
    private volatile List<WorkspaceDto> _workspaces = initial;

    public void SetWorkspaces(List<WorkspaceDto> workspaces) => _workspaces = workspaces;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var content = JsonContent.Create(_workspaces);
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
    }
}
