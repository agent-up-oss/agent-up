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

        if (request.Method == HttpMethod.Post && TryGetWorkspaceAction(path, out var actionId, out var action))
        {
            switch (action)
            {
                case WorkspaceAction.Start:
                    UpdateWorkspaceState(actionId, "Running");
                    return Task.FromResult(NoContent());
                case WorkspaceAction.Stop:
                    UpdateWorkspaceState(actionId, "Stopped");
                    return Task.FromResult(NoContent());
            }
        }

        if (request.Method == HttpMethod.Delete && TryGetSingleWorkspaceId(path, out var deleteId))
        {
            _workspaces = _workspaces.Where(w => w.Id != deleteId).ToList();
            return Task.FromResult(NoContent());
        }

        if (request.Method == HttpMethod.Get && TryGetSingleWorkspaceId(path, out var id))
        {
            var workspace = _workspaces.FirstOrDefault(w => w.Id == id);
            return workspace is null
                ? Task.FromResult(NotFound())
                : Task.FromResult(Ok(workspace));
        }

        return Task.FromResult(Ok(_workspaces));
    }

    private void UpdateWorkspaceState(string id, string state)
    {
        _workspaces = _workspaces
            .Select(workspace => workspace.Id == id ? workspace with { State = state } : workspace)
            .ToList();
    }

    private static bool TryGetWorkspaceAction(string path, out string id, out WorkspaceAction action)
    {
        id = string.Empty;
        action = WorkspaceAction.Start;
        const string prefix = "/api/workspaces/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var remaining = path[prefix.Length..];
        var slashIndex = remaining.IndexOf('/');
        if (slashIndex <= 0)
            return false;

        id = Uri.UnescapeDataString(remaining[..slashIndex]);
        var suffix = remaining[(slashIndex + 1)..];
        if (suffix.Equals("start", StringComparison.Ordinal))
        {
            action = WorkspaceAction.Start;
            return true;
        }

        if (suffix.Equals("stop", StringComparison.Ordinal))
        {
            action = WorkspaceAction.Stop;
            return true;
        }

        return false;
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

    private enum WorkspaceAction
    {
        Start,
        Stop
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage NotFound() =>
        new(System.Net.HttpStatusCode.NotFound);

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage NoContent() =>
        new(System.Net.HttpStatusCode.NoContent);

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage Ok<T>(T value) =>
        new(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
