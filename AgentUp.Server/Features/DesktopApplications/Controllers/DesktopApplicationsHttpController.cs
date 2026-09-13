using AgentUp.Server.Features.DesktopApplications.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.DesktopApplications.Controllers;

[ApiController]
[Route("api/desktop-applications")]
public sealed class DesktopApplicationsHttpController(DesktopApplicationsController desktopApplications) : ControllerBase
{
    [HttpGet("{workspaceId}/{application}")]
    public IActionResult Get(string workspaceId, string application)
    {
        var session = desktopApplications.Get(workspaceId, application);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPost("{workspaceId}/{application}/viewer-ticket")]
    public IActionResult CreateViewerTicket(string workspaceId, string application)
    {
        var ticket = desktopApplications.CreateViewerTicket(workspaceId, application);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [AllowAnonymous]
    [HttpGet("session/{sessionId}/viewer")]
    public IActionResult Viewer(string sessionId, [FromQuery] string? ticket)
    {
        var page = desktopApplications.CreateViewerPage(sessionId, ticket);
        return page is null ? Unauthorized() : Content(page, "text/html");
    }

    [AllowAnonymous]
    [HttpGet("session/{sessionId}/display")]
    public async Task Stream(string sessionId, [FromQuery] string? ticket)
    {
        var status = desktopApplications.ValidateStreamRequest(sessionId, ticket, HttpContext.WebSockets.IsWebSocketRequest);
        if (status != StatusCodes.Status101SwitchingProtocols) { Response.StatusCode = status; return; }
        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await desktopApplications.StreamAsync(sessionId, socket, HttpContext.RequestAborted);
    }

    [HttpGet("{workspaceId}/{application}/screenshot")]
    public async Task<IActionResult> Screenshot(string workspaceId, string application, [FromQuery] long? generation)
    {
        var frame = await desktopApplications.CaptureAsync(workspaceId, application, generation, HttpContext.RequestAborted);
        Response.Headers.CacheControl = "no-store";
        return File(frame, "image/png");
    }

    [HttpPost("{workspaceId}/{application}/pointer-down")]
    public Task PointerDown(string workspaceId, string application, [FromBody] DesktopPointerRequest request) =>
        desktopApplications.PointerAsync(workspaceId, application, request, true, HttpContext.RequestAborted);

    [HttpPost("{workspaceId}/{application}/pointer-up")]
    public Task PointerUp(string workspaceId, string application, [FromBody] DesktopPointerRequest request) =>
        desktopApplications.PointerAsync(workspaceId, application, request, false, HttpContext.RequestAborted);

    [HttpPost("{workspaceId}/{application}/key")]
    public Task Key(string workspaceId, string application, [FromBody] DesktopKeyRequest request) =>
        desktopApplications.KeyAsync(workspaceId, application, request, HttpContext.RequestAborted);
}
