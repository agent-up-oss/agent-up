using AgentUp.Server.Features.TraySession.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.TraySession.Controllers;

[ApiController]
[Route("api/tray")]
public sealed class TraySessionController : ControllerBase
{
    private readonly TrayHeartbeatMonitor _monitor;

    public TraySessionController(TrayHeartbeatMonitor monitor) => _monitor = monitor;

    [HttpPost("heartbeat")]
    public IActionResult PostHeartbeat()
    {
        _monitor.RegisterHeartbeat();
        return Ok();
    }
}
