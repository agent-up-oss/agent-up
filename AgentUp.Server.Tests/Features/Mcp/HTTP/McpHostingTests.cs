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

namespace AgentUp.Server.Tests.Features.Mcp.HTTP;

[TestFixture]
public sealed class McpHostingTests
{
    [Test]
    public void MapMcp_MapsStreamableHttpAndLegacySseEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMcpServer()
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
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.That(endpoints, Does.Contain("/mcp/"));
        Assert.That(endpoints, Does.Contain("/mcp/sse"));
        Assert.That(endpoints, Does.Contain("/mcp/message"));
    }
}
