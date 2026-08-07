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
        await eventBus.StreamToResponseAsync(Response, ct);
    }
}
