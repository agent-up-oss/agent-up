using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Applications.Controllers;

[ApiController]
[Route("api/workspaces")]
public sealed class ApplicationsController(ApplicationLifecycleService lifecycle) : ControllerBase
{
    [HttpGet("{id}/applications")]
    public IActionResult GetApplications(string id)
    {
        var applications = lifecycle.GetApplications(id);
        return applications is null ? NotFound() : Ok(applications);
    }

    [HttpPost("{id}/applications/{name}/start")]
    public async Task<IActionResult> StartApplication(string id, string name)
        => LifecycleResult(this, await lifecycle.StartAsync(id, name));

    [HttpPost("{id}/applications/{name}/stop")]
    public async Task<IActionResult> StopApplication(string id, string name)
        => FoundResult(this, await lifecycle.StopAsync(id, name));

    [HttpPost("{id}/applications/{name}/restart")]
    public async Task<IActionResult> RestartApplication(string id, string name)
        => LifecycleResult(this, await lifecycle.RestartAsync(id, name));

    [HttpGet("{id}/applications/{name}/output")]
    public async Task<IActionResult> GetOutput(string id, string name)
    {
        var result = await lifecycle.GetOutputAsync(id, name);
        return result.Found ? Ok(result.Lines) : NotFound();
    }

    private static IActionResult LifecycleResult(ControllerBase controller, ApplicationLifecycleResult result)
    {
        if (!result.Found)
            return controller.NotFound();

        return result.Succeeded ? controller.NoContent() : controller.Problem(detail: result.Error, statusCode: 500);
    }

    private static IActionResult FoundResult(ControllerBase controller, ApplicationLifecycleResult result)
        => result.Found ? controller.NoContent() : controller.NotFound();
}
