using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[Route("api/browser")]
public sealed class BrowserEventStreamController(BrowserEventBus eventBus) : ControllerBase
{
    [HttpGet("events")]
    public async Task StreamAsync(CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await using var sub = eventBus.Subscribe();
        try
        {
            await foreach (var json in sub.Reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
