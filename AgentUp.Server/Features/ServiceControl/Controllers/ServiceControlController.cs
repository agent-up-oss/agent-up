using AgentUp.Server.Features.ServiceControl.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.ServiceControl.Controllers;

[ApiController]
[Route("api/service")]
public sealed class ServiceControlController : ControllerBase
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IProcessExitCode _exitCode;

    public ServiceControlController(IHostApplicationLifetime lifetime, IProcessExitCode exitCode)
    {
        _lifetime = lifetime;
        _exitCode = exitCode;
    }

    // Asks the daemon manager to restart the server (exits with non-zero so systemd/launchd restarts).
    [HttpPost("restart")]
    public IActionResult Restart()
    {
        _exitCode.Set(1);
        ScheduleStop();
        return Accepted();
    }

    // Stops the server cleanly (exits with zero so systemd/launchd does NOT restart).
    [HttpPost("shutdown")]
    public IActionResult Shutdown()
    {
        _exitCode.Set(0);
        ScheduleStop();
        return Accepted();
    }

    private void ScheduleStop()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            _lifetime.StopApplication();
        });
    }
}
