using System.Net.Http.Json;
using AgentUp.Desktop.Features.Workspaces.Http;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class FakeHttpMessageHandler(
    List<WorkspaceDto> workspaces,
    Dictionary<string, List<string>>? outputLines = null) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        if (outputLines is not null && path.EndsWith("/output"))
        {
            var key = ExtractOutputKey(path);
            var lines = outputLines.GetValueOrDefault(key, []);
            return Task.FromResult(Ok(lines));
        }

        return Task.FromResult(Ok(workspaces));
    }

    private static HttpResponseMessage Ok<T>(T value) =>
        new(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    // /api/workspaces/{id}/applications/{name}/output → "id/name"
    private static string ExtractOutputKey(string path)
    {
        var parts = path.Trim('/').Split('/');
        return parts.Length >= 6 ? $"{parts[2]}/{parts[4]}" : string.Empty;
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

    public int RequestCount { get; private set; }

    public void SetWorkspaces(List<WorkspaceDto> workspaces) => _workspaces = workspaces;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        RequestCount++;
        var content = JsonContent.Create(_workspaces);
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
    }
}
