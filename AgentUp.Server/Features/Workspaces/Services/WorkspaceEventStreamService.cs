using AgentUp.Server.Features.Workspaces.Providers;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceEventStreamService(
    WorkspaceEventBus eventBus,
    WorkspaceEventFrameProvider frames)
{
    public async Task WriteAsync(HttpResponse response, CancellationToken ct)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";

        await using var sub = eventBus.Subscribe();
        try
        {
            await foreach (var evt in sub.Reader.ReadAllAsync(ct))
            {
                await response.WriteAsync(frames.Format(evt), ct);
                await response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
    }
}
