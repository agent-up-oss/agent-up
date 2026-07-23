using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Workspaces.Controllers;

[ApiController]
[Route("api/workspaces")]
public sealed class WorkspacesController(WorkspaceRegistry registry, WorkspaceLifecycleService lifecycle) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(registry.GetAll());

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var workspace = registry.GetById(id);
        return workspace is null ? NotFound() : Ok(workspace);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterWorkspaceRequest request)
    {
        var workspace = await registry.RegisterAsync(request);
        return Created($"/api/workspaces/{workspace.Id}", workspace);
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> Start(string id)
        => LifecycleResult(this, await lifecycle.StartAsync(id));

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> Stop(string id)
        => FoundResult(this, await lifecycle.StopAsync(id));

    [HttpPost("tutorial/cleanup")]
    public async Task<IActionResult> CleanupTutorialWorkspaces()
    {
        var removed = await lifecycle.CleanupTutorialWorkspacesAsync();
        return Ok(new { removed });
    }

    [HttpPatch("{id}/state")]
    public async Task<IActionResult> UpdateState(string id, UpdateWorkspaceStateRequest request)
        => BoolResult(this, await registry.UpdateStateAsync(id, request.State));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
        => BoolResult(this, await registry.RemoveAsync(id));

    private static IActionResult LifecycleResult(ControllerBase controller, WorkspaceLifecycleResult result)
    {
        if (!result.Found)
            return controller.NotFound();

        return result.Succeeded ? controller.NoContent() : controller.Problem(detail: result.Error, statusCode: 500);
    }

    private static IActionResult FoundResult(ControllerBase controller, WorkspaceLifecycleResult result)
        => result.Found ? controller.NoContent() : controller.NotFound();

    private static IActionResult BoolResult(ControllerBase controller, bool found)
        => found ? controller.NoContent() : controller.NotFound();
}
