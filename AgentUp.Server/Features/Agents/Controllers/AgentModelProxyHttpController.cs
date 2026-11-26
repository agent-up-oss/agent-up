using AgentUp.Server.Features.Agents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Agents.Controllers;

/// <summary>
/// Loopback-only model HTTP for a sandboxed agent. The key in the path is what the sandbox
/// process was given; the Server then talks to the real origin.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/agent-model")]
public sealed class AgentModelProxyHttpController(AgentModelProxyService proxy) : ControllerBase
{
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("{key}/{capability}/{**path}")]
    public Task Forward(string key, string capability, string? path)
        => proxy.ForwardAsync(key, capability, path, HttpContext);

    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("{key}/{capability}")]
    public Task ForwardRoot(string key, string capability)
        => proxy.ForwardAsync(key, capability, string.Empty, HttpContext);
}
