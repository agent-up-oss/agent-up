using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Applications.Controllers;

[ApiController]
[Route("api/workspaces")]
public sealed class ApplicationsController(WorkspaceRegistry registry, IWorkspaceProcessManager processes, IOutputRepository output) : ControllerBase
{
    [HttpGet("{id}/applications")]
    public IActionResult GetApplications(string id)
    {
        var workspace = registry.GetById(id);
        return workspace is null ? NotFound() : Ok(workspace.Applications);
    }

    [HttpPost("{id}/applications/{name}/start")]
    public async Task<IActionResult> StartApplication(string id, string name)
    {
        var workspace = registry.GetById(id);
        if (workspace is null) return NotFound();
        if (workspace.Applications.All(a => a.Name != name)) return NotFound();

        await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Starting);
        try
        {
            await processes.LaunchApplicationAsync(workspace, name);
            await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Running);
            return NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Failed);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("{id}/applications/{name}/stop")]
    public async Task<IActionResult> StopApplication(string id, string name)
    {
        var workspace = registry.GetById(id);
        if (workspace is null) return NotFound();
        if (workspace.Applications.All(a => a.Name != name)) return NotFound();

        await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Stopping);
        await processes.KillApplicationAsync(id, name);
        await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Stopped);
        return NoContent();
    }

    [HttpPost("{id}/applications/{name}/restart")]
    public async Task<IActionResult> RestartApplication(string id, string name)
    {
        var workspace = registry.GetById(id);
        if (workspace is null) return NotFound();
        if (workspace.Applications.All(a => a.Name != name)) return NotFound();

        await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Starting);
        try
        {
            await processes.KillApplicationAsync(id, name);
            await processes.LaunchApplicationAsync(workspace, name);
            await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Running);
            return NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Failed);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpGet("{id}/applications/{name}/output")]
    public async Task<IActionResult> GetOutput(string id, string name)
    {
        var workspace = registry.GetById(id);
        if (workspace is null) return NotFound();
        if (workspace.Applications.All(a => a.Name != name)) return NotFound();

        var lines = await output.GetAsync(id, name);
        return Ok(lines);
    }
}
