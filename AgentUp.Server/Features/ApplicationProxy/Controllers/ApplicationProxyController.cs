using AgentUp.Server.Features.ApplicationProxy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.ApplicationProxy.Controllers;

[ApiController]
[AllowAnonymous]
[Route("apps/{workspaceId}/{port:int}")]
public sealed class ApplicationProxyController(ApplicationProxyService proxy) : ControllerBase
{
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("{**path}")]
    public Task Open(string workspaceId, int port, string? path)
        => proxy.OpenAsync(workspaceId, port, path, HttpContext);

    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("")]
    public Task OpenRoot(string workspaceId, int port)
        => proxy.OpenAsync(workspaceId, port, string.Empty, HttpContext);
}
