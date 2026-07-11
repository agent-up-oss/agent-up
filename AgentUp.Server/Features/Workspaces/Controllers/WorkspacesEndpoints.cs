using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Workspaces.Controllers;

public static class WorkspacesEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaces(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces");

        group.MapGet("/", (IWorkspaceRegistry registry) =>
            Results.Ok(registry.GetAll()));

        group.MapGet("/{id}", (string id, IWorkspaceRegistry registry) =>
        {
            var workspace = registry.GetById(id);
            return workspace is null ? Results.NotFound() : Results.Ok(workspace);
        });

        group.MapPost("/", async (RegisterWorkspaceRequest request, IWorkspaceRegistry registry) =>
        {
            var workspace = await registry.RegisterAsync(request);
            return Results.Created($"/api/workspaces/{workspace.Id}", workspace);
        });

        group.MapGet("/{id}/applications", (string id, IWorkspaceRegistry registry) =>
        {
            var workspace = registry.GetById(id);
            return workspace is null ? Results.NotFound() : Results.Ok(workspace.Applications);
        });

        group.MapPost("/{id}/start", async (string id, IWorkspaceRegistry registry, IWorkspaceProcessManager processes) =>
        {
            var workspace = registry.GetById(id);
            if (workspace is null) return Results.NotFound();

            await registry.UpdateStateAsync(id, WorkspaceState.Starting);
            try
            {
                await processes.LaunchAsync(workspace);
                await registry.UpdateStateAsync(id, WorkspaceState.Running);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                await registry.UpdateStateAsync(id, WorkspaceState.Failed);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        group.MapPost("/{id}/stop", async (string id, IWorkspaceRegistry registry, IWorkspaceProcessManager processes) =>
        {
            var workspace = registry.GetById(id);
            if (workspace is null) return Results.NotFound();

            await registry.UpdateStateAsync(id, WorkspaceState.Stopping);
            await processes.KillAsync(id);
            await registry.UpdateStateAsync(id, WorkspaceState.Stopped);
            return Results.NoContent();
        });

        group.MapPatch("/{id}/state", async (string id, [FromBody] UpdateWorkspaceStateRequest request, IWorkspaceRegistry registry) =>
        {
            var updated = await registry.UpdateStateAsync(id, request.State);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id}", async (string id, IWorkspaceRegistry registry) =>
        {
            var removed = await registry.RemoveAsync(id);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
