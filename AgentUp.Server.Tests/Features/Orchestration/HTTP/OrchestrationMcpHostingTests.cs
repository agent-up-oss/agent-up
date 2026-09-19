using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Providers;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Features.Diagnostics.Controllers;
using AgentUp.Server.Features.Diagnostics.Services;
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
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Shared.Providers;
using Microsoft.AspNetCore.Http;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Orchestration.HTTP;

[TestFixture]
public sealed class OrchestrationMcpHostingTests
{
    [Test]
    public async Task CommitsTransport_GuardReturnsManagedWorktreeAsStructuredJsonRpcData()
    {
        var queue = new TransportQueueProvider(ServerDomain.Queue()
            .AtVersion(3)
            .With(ServerDomain.CommitEntry()
                .For("Commits")
                .Saying("feat(commits): queued")
                .Touching(["a.cs"])
                .WithId("entry")
                .WithPatchId("patch")
                .WithParentCommit("base")
                .WithProposalCommit("tip")
                .InState("ready")
                .Build())
            .WithBaseCommit("base")
            .WithTipCommit("tip")
            .InWorktree("/managed/queue")
            .AtGeneration(1)
            .Build());
        await using var app = BuildMcpApp(queue, new TransportGitProvider());
        app.MapMcp("/mcp/commits");
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");

        using var initialize = await client.PostAsJsonAsync("/mcp/commits", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "mock-acp", version = "1" } }
        });
        initialize.EnsureSuccessStatusCode();
        var session = initialize.Headers.GetValues("Mcp-Session-Id").Single();

        using var call = new HttpRequestMessage(HttpMethod.Post, "/mcp/commits")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new { name = "guard_commits", arguments = new { worktreePath = "/repos/app" } }
            })
        };
        call.Headers.Add("Mcp-Session-Id", session);
        call.Headers.Accept.ParseAdd("application/json");
        call.Headers.Accept.ParseAdd("text/event-stream");
        using var response = await client.SendAsync(call);
        var payload = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.That(payload, Does.Contain("/managed/queue"));
        Assert.That(payload, Does.Contain("continueWorktreePath"));
        Assert.That(payload, Does.Contain("Continue dependent work"));
    }

    // Every MCP server gets its own route prefix with both transports under it, rather than
    // one shared "/mcp" that would expose every slice's tools to every client.
    [TestCase("/mcp/commits")]
    [TestCase("/mcp/orchestration")]
    [TestCase("/mcp/browser")]
    [TestCase("/mcp/audit")]
    public void MapMcp_MapsStreamableHttpAndLegacySseUnderEachServerPrefix(string prefix)
    {
        var endpoints = MappedEndpoints();

        Assert.Multiple(() =>
        {
            Assert.That(endpoints, Does.Contain(prefix));
            Assert.That(endpoints, Does.Contain($"{prefix}/sse"));
            Assert.That(endpoints, Does.Contain($"{prefix}/message"));
        });
    }

    [Test]
    public void MapMcp_DoesNotMapASharedRootEndpoint()
        => Assert.That(MappedEndpoints(), Does.Not.Contain("/mcp"));

    private static string[] MappedEndpoints()
    {
        using var app = BuildMcpApp();
        app.MapMcp("/mcp/commits");
        app.MapMcp("/mcp/orchestration");
        app.MapMcp("/mcp/browser");
        app.MapMcp("/mcp/audit");

        return ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => NormalizeRoutePattern(endpoint.RoutePattern.RawText))
            .ToArray();
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
        Assert.That(tools, Does.Contain("get_workspace_diagnostics"));
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

    private static WebApplication BuildMcpApp(ICommitsQueueProvider? queue = null, ICommitsGitProvider? commitsGit = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
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
            .WithTools<DiagnosticsMcpTools>()
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
        builder.Services.AddSingleton<BrowserRemoteDisplayService>();
        builder.Services.AddSingleton<BrowserEventBus>();
        builder.Services.AddSingleton<AppHealthCheckService>();
        builder.Services.AddSingleton<AppHealthController>();
        builder.Services.AddSingleton<IAuditEventRepository, InMemoryAuditEventRepository>();
        builder.Services.AddSingleton<IAuditArtifactRepository, InMemoryAuditArtifactRepository>();
        builder.Services.AddSingleton<IAuditIdentityProvider, FakeAuditIdentityProvider>();
        builder.Services.AddSingleton<AuditEventBus>();
        builder.Services.AddSingleton<AuditService>();
        builder.Services.AddSingleton<AuditController>();
        builder.Services.AddSingleton(sp => new WorkspaceStreamStateService(
            sp.GetRequiredService<BrowserEventBus>(),
            sp.GetRequiredService<AppHealthController>(),
            sp.GetRequiredService<WorkspaceQueryController>(),
            sp.GetRequiredService<AuditController>(),
            sp.GetRequiredService<ILogger<WorkspaceStreamStateService>>()));
        builder.Services.AddSingleton(sp => new HeadlessBrowserSessionManager(
            Path.GetTempPath(), Path.GetTempPath(),
            sp.GetRequiredService<BrowserRemoteDisplayService>(),
            sp.GetRequiredService<WorkspaceStreamStateService>(),
            sp.GetRequiredService<ILogger<HeadlessBrowserSessionManager>>()));
        builder.Services.AddSingleton<WorkspaceStreamStateController>();
        builder.Services.AddSingleton<BrowserMcpService>();
        builder.Services.AddSingleton<IAgentUpConfigurationProvider, AgentUpConfigurationProvider>();
        builder.Services.AddSingleton<IWorkspaceIdentityProvider, GitWorkspaceIdentityProvider>();
        builder.Services.AddSingleton<IAgentUpContextProvider, AgentUpContextProvider>();
        builder.Services.AddSingleton<OrchestrationContextService>();
        builder.Services.AddWorkspaceLifecycleSupport();
        builder.Services.AddSingleton<OrchestrationWorkspaceService>();
        builder.Services.AddSingleton<ConsoleSecretRedactor>();
        builder.Services.AddSingleton<OrchestrationConsoleService>();
        builder.Services.AddSingleton<OrchestrationWorkspaceController>();
        builder.Services.AddSingleton<OrchestrationContextController>();
        builder.Services.AddSingleton<OrchestrationConsoleController>();
        var gitProvider = commitsGit ?? new CommitsGitProvider();
        builder.Services.AddSingleton<ICommitsGitProvider>(gitProvider);
        builder.Services.AddSingleton<ICommitsQueueProvider>(queue ?? new CommitsQueueProvider(gitProvider));
        builder.Services.AddSingleton<CommitPolicyProvider>();
        builder.Services.AddSingleton<CommitsService>();
        builder.Services.AddSingleton<CommitsController>();
        builder.Services.AddSingleton<CommitQueueMcpService>();
        builder.Services.AddSingleton<AuditController>(sp => ServerTestComposition.CreateAuditController());
        builder.Services.AddSingleton<WorkspaceDiagnosticsService>();
        builder.Services.AddSingleton<WorkspaceDiagnosticsController>();
        builder.Services.AddSingleton<McpEndpointSessionProvider>();

        return builder.Build();
    }

    private sealed class TransportQueueProvider(CommitsQueue stored) : ICommitsQueueProvider
    {
        public Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult(stored);
        public Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default) { stored = queue; return Task.CompletedTask; }
        public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    private sealed class TransportGitProvider : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult(worktreePath);
        public Task<string> GetRepositoryIdentityAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult(worktreePath);
        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult(GitOperationState.None);
        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static string NormalizeRoutePattern(string? routePattern)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
            return string.Empty;

        var normalized = routePattern.TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }
}
