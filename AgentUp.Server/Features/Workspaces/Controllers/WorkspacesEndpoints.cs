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

        group.MapPost("/", (RegisterWorkspaceRequest request, IWorkspaceRegistry registry) =>
        {
            var workspace = registry.Register(request);
            return Results.Created($"/api/workspaces/{workspace.Id}", workspace);
        });

        group.MapPatch("/{id}/state", (string id, [FromBody] UpdateWorkspaceStateRequest request, IWorkspaceRegistry registry) =>
        {
            var updated = registry.UpdateState(id, request.State);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id}", (string id, IWorkspaceRegistry registry) =>
        {
            var removed = registry.Remove(id);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
