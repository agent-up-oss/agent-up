using System.Net.Http.Json;
using System.Diagnostics.CodeAnalysis;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;

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
    public List<string> RequestPaths { get; } = [];

    public void SetWorkspaces(List<WorkspaceDto> workspaces) => _workspaces = workspaces;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        RequestCount++;
        var path = request.RequestUri?.AbsolutePath ?? "";
        RequestPaths.Add(path);

        if (request.Method == HttpMethod.Get && TryGetSingleWorkspaceId(path, out var id))
        {
            var workspace = _workspaces.FirstOrDefault(w => w.Id == id);
            return workspace is null
                ? Task.FromResult(NotFound())
                : Task.FromResult(Ok(workspace));
        }

        return Task.FromResult(Ok(_workspaces));
    }

    private static bool TryGetSingleWorkspaceId(string path, out string id)
    {
        id = string.Empty;
        const string prefix = "/api/workspaces/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var remaining = path[prefix.Length..];
        if (remaining.Length == 0 || remaining.Contains('/'))
            return false;

        id = Uri.UnescapeDataString(remaining);
        return true;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage NotFound() =>
        new(System.Net.HttpStatusCode.NotFound);

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage Ok<T>(T value) =>
        new(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
