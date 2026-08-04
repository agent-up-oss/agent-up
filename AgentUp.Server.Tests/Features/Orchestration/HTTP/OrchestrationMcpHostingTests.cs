using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Providers;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Services;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Server.Shared.Providers;
using Microsoft.AspNetCore.Http;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Orchestration.HTTP;

[TestFixture]
public sealed class OrchestrationMcpHostingTests
{
    [Test]
    public void MapMcp_MapsSeparateStreamableHttpAndLegacySseEndpoints()
    {
        using var app = BuildMcpApp();
        app.MapMcp("/mcp/commits");
        app.MapMcp("/mcp/orchestration");
        app.MapMcp("/mcp/browser");
        app.MapMcp("/mcp/audit");

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => NormalizeRoutePattern(endpoint.RoutePattern.RawText))
            .ToArray();

        Assert.That(endpoints, Does.Not.Contain("/mcp"));
        Assert.That(endpoints, Does.Contain("/mcp/commits"));
        Assert.That(endpoints, Does.Contain("/mcp/commits/sse"));
        Assert.That(endpoints, Does.Contain("/mcp/commits/message"));
        Assert.That(endpoints, Does.Contain("/mcp/orchestration"));
        Assert.That(endpoints, Does.Contain("/mcp/orchestration/sse"));
        Assert.That(endpoints, Does.Contain("/mcp/orchestration/message"));
        Assert.That(endpoints, Does.Contain("/mcp/browser"));
        Assert.That(endpoints, Does.Contain("/mcp/audit"));
        Assert.That(endpoints, Does.Contain("/mcp/audit/sse"));
        Assert.That(endpoints, Does.Contain("/mcp/audit/message"));
    }

    [Test]
    public async Task ConfigureSessionOptions_CommitsEndpoint_ExposesOnlyCommitQueueTools()
    {
        using var app = BuildMcpApp();
        var options = app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var transport = app.Services.GetRequiredService<IOptions<HttpServerTransportOptions>>().Value;
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Path = "/mcp/commits";

        await transport.ConfigureSessionOptions!(context, options, CancellationToken.None);

        var tools = options.ToolCollection?.PrimitiveNames.ToArray() ?? [];
        Assert.That(tools, Does.Contain("enqueue_commit"));
        Assert.That(tools, Does.Contain("guard_commits"));
        Assert.That(tools, Does.Not.Contain("start_workspace"));
        Assert.That(options.ResourceCollection?.PrimitiveNames ?? [], Is.Empty);
        Assert.That(options.ServerInstructions, Does.Contain("commit queue MCP server"));
    }

    [Test]
    public async Task ConfigureSessionOptions_OrchestrationEndpoint_ExposesOnlyOrchestrationTools()
    {
        using var app = BuildMcpApp();
        var options = app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var transport = app.Services.GetRequiredService<IOptions<HttpServerTransportOptions>>().Value;
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Path = "/mcp/orchestration";

        await transport.ConfigureSessionOptions!(context, options, CancellationToken.None);

        var tools = options.ToolCollection?.PrimitiveNames.ToArray() ?? [];
        Assert.That(tools, Does.Contain("start_workspace"));
        Assert.That(tools, Does.Contain("get_workspace_console"));
        Assert.That(tools, Does.Contain("get_agent_up_context"));
        Assert.That(tools, Does.Not.Contain("enqueue_commit"));
        Assert.That(options.ResourceCollection?.PrimitiveNames ?? [], Does.Contain("agent-up://context"));
    }

    [Test]
    public async Task ConfigureSessionOptions_AuditEndpoint_ExposesOnlyAuditTools()
    {
        using var app = BuildMcpApp();
        var options = app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var transport = app.Services.GetRequiredService<IOptions<HttpServerTransportOptions>>().Value;
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Path = "/mcp/audit";

        await transport.ConfigureSessionOptions!(context, options, CancellationToken.None);

        var tools = options.ToolCollection?.PrimitiveNames.ToArray() ?? [];
        Assert.That(tools, Is.EquivalentTo(new[]
        {
            "audit_query",
            "audit_timeline",
            "audit_get_event",
            "audit_load_artifact"
        }));
        Assert.That(options.ResourceCollection?.PrimitiveNames ?? [], Is.Empty);
    }

    private static WebApplication BuildMcpApp()
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
                options.ConfigureSessionOptions = (context, serverOptions, ct) =>
                    context.RequestServices.GetRequiredService<McpEndpointSessionProvider>()
                        .ConfigureAsync(context, serverOptions, ct);
            })
            .WithTools<CommitQueueMcpTools>()
            .WithTools<OrchestrationMcpTools>()
            .WithTools<BrowserMcpTools>()
            .WithTools<AuditMcpTools>()
            .WithResources<OrchestrationMcpResources>();
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IOutputRepository, InMemoryOutputRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton(_ => new CapabilityReconciliationService([]));
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<WorkspaceEventBus>();
        builder.Services.AddSingleton<AgentUp.Server.Features.Workspaces.Providers.WorkspaceEventFrameProvider>();
        builder.Services.AddSingleton<WorkspaceEventStreamService>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddSingleton<IWorkspaceProcessManager, NullWorkspaceProcessManager>();
        builder.Services.AddSingleton<ProcessOutputService>();
        builder.Services.AddSingleton<ProcessesController>();
        builder.Services.AddSingleton<WorkspaceStateController>();
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<BrowserSessionStore>();
        builder.Services.AddSingleton<BrowserMcpService>();
        builder.Services.AddSingleton<IAgentUpConfigurationProvider, AgentUpConfigurationProvider>();
        builder.Services.AddSingleton<IWorkspaceIdentityProvider, GitWorkspaceIdentityProvider>();
        builder.Services.AddSingleton<IAgentUpContextProvider, AgentUpContextProvider>();
        builder.Services.AddSingleton<OrchestrationContextService>();
        builder.Services.AddSingleton<OrchestrationWorkspaceService>();
        builder.Services.AddSingleton<ConsoleSecretRedactor>();
        builder.Services.AddSingleton<OrchestrationConsoleService>();
        builder.Services.AddSingleton<OrchestrationWorkspaceController>();
        builder.Services.AddSingleton<OrchestrationContextController>();
        builder.Services.AddSingleton<OrchestrationConsoleController>();
        builder.Services.AddSingleton<ICommitsGitProvider, CommitsGitProvider>();
        builder.Services.AddSingleton<ICommitsQueueProvider, CommitsQueueProvider>();
        builder.Services.AddSingleton<CommitPolicyProvider>();
        builder.Services.AddSingleton<CommitsService>();
        builder.Services.AddSingleton<CommitsController>();
        builder.Services.AddSingleton<CommitQueueMcpService>();
        builder.Services.AddSingleton<AuditController>(sp => ServerTestComposition.CreateAuditController());
        builder.Services.AddSingleton<McpEndpointSessionProvider>();

        return builder.Build();
    }

    private static string NormalizeRoutePattern(string? routePattern)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
            return string.Empty;

        var normalized = routePattern.TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }
}
