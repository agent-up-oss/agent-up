using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Workspaces.Controllers;

[ApiController]
[Route("api/workspaces")]
public sealed class WorkspacesController(IWorkspaceRegistry registry, IWorkspaceProcessManager processes) : ControllerBase
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
        var workspace = registry.GetById(id);
        if (workspace is null) return NotFound();

        await registry.UpdateStateAsync(id, WorkspaceState.Starting);
        foreach (var app in workspace.Applications)
            await registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Starting);
        try
        {
            await processes.LaunchAsync(workspace);
            await registry.UpdateStateAsync(id, WorkspaceState.Running);
            foreach (var app in workspace.Applications)
                await registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Running);
            return NoContent();
        }
        catch (Exception ex)
        {
            await registry.UpdateStateAsync(id, WorkspaceState.Failed);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> Stop(string id)
    {
        var workspace = registry.GetById(id);
        if (workspace is null) return NotFound();

        await registry.UpdateStateAsync(id, WorkspaceState.Stopping);
        foreach (var app in workspace.Applications)
            await registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Stopping);
        await processes.KillAsync(id);
        await registry.UpdateStateAsync(id, WorkspaceState.Stopped);
        foreach (var app in workspace.Applications)
            await registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Stopped);
        return NoContent();
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
