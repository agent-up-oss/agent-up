using AgentUp.Server.Features.Mcp.Controllers;
using AgentUp.Server.Features.Mcp.Interfaces;
using AgentUp.Server.Features.Mcp.Providers;
using AgentUp.Server.Features.Mcp.Resources;
using AgentUp.Server.Features.Mcp.Services;
using AgentUp.Server.Features.Mcp.Tools;
using AgentUp.Server.Features.Ports.Services;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Mcp.HTTP;

[TestFixture]
public sealed class McpHostingTests
{
    [Test]
    public void MapMcp_MapsStreamableHttpAndLegacySseEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMcpServer(options =>
            {
                options.ServerInstructions = AgentUpMcpGuidance.ServerInstructions;
            })
            .WithHttpTransport(options =>
            {
                options.Stateless = false;
#pragma warning disable MCP9004 // Intentional compatibility coverage for legacy local MCP clients.
                options.EnableLegacySse = true;
#pragma warning restore MCP9004
            })
            .WithTools<AgentUpMcpTools>()
            .WithResources<AgentUpMcpResources>();
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddSingleton<IWorkspaceProcessManager, NullWorkspaceProcessManager>();
        builder.Services.AddSingleton<IAgentUpConfigurationProvider, AgentUpConfigurationProvider>();
        builder.Services.AddSingleton<IWorkspaceIdentityProvider, GitWorkspaceIdentityProvider>();
        builder.Services.AddSingleton<IAgentUpContextProvider, AgentUpContextProvider>();
        builder.Services.AddSingleton<McpWorkspaceService>();
        builder.Services.AddSingleton<McpWorkspaceController>();
        builder.Services.AddSingleton<McpContextController>();

        using var app = builder.Build();
        app.MapMcp("/mcp");

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => NormalizeRoutePattern(endpoint.RoutePattern.RawText))
            .ToArray();

        Assert.That(endpoints, Does.Contain("/mcp"));
        Assert.That(endpoints, Does.Contain("/mcp/sse"));
        Assert.That(endpoints, Does.Contain("/mcp/message"));

        var options = app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        Assert.That(options.ServerInstructions, Does.Contain("deploy my app with Agent-Up"));
        Assert.That(options.ServerInstructions, Does.Contain("call start_workspace"));
        Assert.That(options.ServerInstructions, Does.Contain("Before using curl"));
    }

    private static string NormalizeRoutePattern(string? routePattern)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
            return string.Empty;

        var normalized = routePattern.TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }
}
