using System.Net;
using System.Net.Http;
using System.Text;
using AgentUp.Desktop.Features.FakeServer.DTOs;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Services;

namespace AgentUp.Desktop.Features.FakeServer.Providers;

public sealed class FakeServerMessageHandler : DelegatingHandler
{
    private readonly FakeBackendService _backend;

    public FakeServerMessageHandler(FakeBackendService backend, HttpMessageHandler inner)
        : base(inner)
    {
        _backend = backend;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!FakeServerIdentity.Matches(request.RequestUri))
            return await base.SendAsync(request, cancellationToken);

        var path = request.RequestUri?.AbsolutePath ?? "/";
        var query = request.RequestUri?.Query ?? "";
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var dto = new FakeBackendRequestDto(request.Method.Method, path, query, body);

        if (IsAgentEventRoute(path))
            return AgentEventResponse(dto, cancellationToken);
        if (IsWorkspaceEventRoute(path))
            return WorkspaceEventResponse(cancellationToken);

        var result = _backend.Handle(dto);
        return ToResponse(result);
    }

    private HttpResponseMessage AgentEventResponse(FakeBackendRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryWorkspaceId(request.Path, out var workspaceId))
            return ToResponse(_backend.Handle(request));

        var after = 0L;
        _ = long.TryParse(QueryAfter(request.Query), out after);
        var stream = new FakeServerSseStream();
        foreach (var item in _backend.AgentEventsAfter(workspaceId, after))
            stream.WriteFrame(item.ToSseFrame());
        var subscription = _backend.SubscribeAgent(workspaceId, item => stream.WriteFrame(item.ToSseFrame()));
        cancellationToken.Register(() =>
        {
            subscription.Dispose();
            stream.Complete();
        });
        return StreamResponse(stream);
    }

    private HttpResponseMessage WorkspaceEventResponse(CancellationToken cancellationToken)
    {
        var stream = new FakeServerSseStream();
        stream.WriteFrame(_backend.WorkspaceSnapshotSse());
        var subscription = _backend.SubscribeWorkspaces(snapshot => stream.WriteFrame(snapshot));
        cancellationToken.Register(() =>
        {
            subscription.Dispose();
            stream.Complete();
        });
        return StreamResponse(stream);
    }

    private static HttpResponseMessage StreamResponse(FakeServerSseStream stream)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
        return response;
    }

    private static HttpResponseMessage ToResponse(FakeBackendResponseDto result)
    {
        var response = new HttpResponseMessage((HttpStatusCode)result.Status);
        if (result.Body is null)
            return response;

        response.Content = new StringContent(result.Body, Encoding.UTF8, MediaType(result.ContentType));
        return response;
    }

    private static string MediaType(string contentType)
        => contentType.Split(';', 2)[0];

    private static bool IsAgentEventRoute(string path)
        => path.StartsWith("/api/workspaces/", StringComparison.Ordinal)
           && path.EndsWith("/agent/events", StringComparison.Ordinal);

    private static bool IsWorkspaceEventRoute(string path)
        => string.Equals(path.TrimEnd('/'), "/api/workspaces/events", StringComparison.Ordinal);

    private static bool TryWorkspaceId(string path, out string workspaceId)
    {
        workspaceId = "";
        const string prefix = "/api/workspaces/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var remaining = path[prefix.Length..];
        var slash = remaining.IndexOf('/');
        if (slash <= 0)
            return false;
        workspaceId = Uri.UnescapeDataString(remaining[..slash]);
        return workspaceId.Length > 0;
    }

    private static string? QueryAfter(string query)
    {
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        return trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0] == "after")
            .Select(parts => parts[1])
            .FirstOrDefault();
    }
}
