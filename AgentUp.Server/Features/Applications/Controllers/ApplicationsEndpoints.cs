using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Applications.Controllers;

public static class ApplicationsEndpoints
{
    public static IEndpointRouteBuilder MapApplications(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces");

        group.MapGet("/{id}/applications", (string id, IWorkspaceRegistry registry) =>
        {
            var workspace = registry.GetById(id);
            return workspace is null ? Results.NotFound() : Results.Ok(workspace.Applications);
        });

        group.MapPost("/{id}/applications/{name}/start", async (string id, string name, IWorkspaceRegistry registry, IWorkspaceProcessManager processes) =>
        {
            var workspace = registry.GetById(id);
            if (workspace is null) return Results.NotFound();
            if (workspace.Applications.All(a => a.Name != name)) return Results.NotFound();

            await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Starting);
            try
            {
                await processes.LaunchApplicationAsync(workspace, name);
                await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Running);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Failed);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        group.MapPost("/{id}/applications/{name}/stop", async (string id, string name, IWorkspaceRegistry registry, IWorkspaceProcessManager processes) =>
        {
            var workspace = registry.GetById(id);
            if (workspace is null) return Results.NotFound();
            if (workspace.Applications.All(a => a.Name != name)) return Results.NotFound();

            await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Stopping);
            await processes.KillApplicationAsync(id, name);
            await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Stopped);
            return Results.NoContent();
        });

        group.MapPost("/{id}/applications/{name}/restart", async (string id, string name, IWorkspaceRegistry registry, IWorkspaceProcessManager processes) =>
        {
            var workspace = registry.GetById(id);
            if (workspace is null) return Results.NotFound();
            if (workspace.Applications.All(a => a.Name != name)) return Results.NotFound();

            await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Starting);
            try
            {
                await processes.KillApplicationAsync(id, name);
                await processes.LaunchApplicationAsync(workspace, name);
                await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Running);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                await registry.UpdateApplicationStateAsync(id, name, ApplicationState.Failed);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        group.MapGet("/{id}/applications/{name}/output", async (string id, string name, IWorkspaceRegistry registry, IOutputRepository output) =>
        {
            var workspace = registry.GetById(id);
            if (workspace is null) return Results.NotFound();
            if (workspace.Applications.All(a => a.Name != name)) return Results.NotFound();

            var lines = await output.GetAsync(id, name);
            return Results.Ok(lines);
        });

        return app;
    }
}
