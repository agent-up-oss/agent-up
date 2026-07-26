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

    // Restarts the server by exiting immediately without notifying the service manager,
    // causing Windows SCM / systemd / launchd to detect an unexpected exit and restart.
    [HttpPost("restart")]
    public IActionResult Restart()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            _exitCode.Exit(1);
        });
        return Accepted();
    }

    // Stops the server cleanly (exits with zero so the service manager does NOT restart).
    [HttpPost("shutdown")]
    public IActionResult Shutdown()
    {
        _exitCode.Set(0);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            _lifetime.StopApplication();
        });
        return Accepted();
    }
}
