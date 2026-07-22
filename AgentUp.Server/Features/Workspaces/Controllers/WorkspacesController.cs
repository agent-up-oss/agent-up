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
    {
        var result = await lifecycle.StartAsync(id);
        if (!result.Found) return NotFound();
        return result.Succeeded ? NoContent() : Problem(detail: result.Error, statusCode: 500);
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> Stop(string id)
    {
        var result = await lifecycle.StopAsync(id);
        return result.Found ? NoContent() : NotFound();
    }

    [HttpPost("tutorial/cleanup")]
    public async Task<IActionResult> CleanupTutorialWorkspaces()
    {
        var removed = await lifecycle.CleanupTutorialWorkspacesAsync();
        return Ok(new { removed });
    }

    [HttpPatch("{id}/state")]
    public async Task<IActionResult> UpdateState(string id, UpdateWorkspaceStateRequest request)
    {
        var updated = await registry.UpdateStateAsync(id, request.State);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var removed = await registry.RemoveAsync(id);
        return removed ? NoContent() : NotFound();
    }
}
