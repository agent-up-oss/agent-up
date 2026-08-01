using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceEventStreamService(WorkspaceEventBus eventBus)
{
    private static readonly JsonSerializerOptions EventJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
                var json = JsonSerializer.Serialize(evt, EventJsonOptions);
                await response.WriteAsync($"data: {json}\n\n", ct);
                await response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
    }
}
