using System.Text.Json.Serialization;
using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Interfaces;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Interfaces;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Providers;
using AgentUp.Server.Features.Audit.Repositories;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.ServiceControl.Interfaces;
using AgentUp.Server.Features.TraySession.Services;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Browser.Streaming.Interfaces;
using AgentUp.Server.Features.Browser.Providers;
using AgentUp.Browser.Streaming;
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
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Ports.Providers;
using AgentUp.Server.Features.Ports.Services;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Providers;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Providers;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Shared.Providers;

namespace AgentUp.Server.Composition;

public static class ServiceRegistration
{
    public static void Configure(WebApplicationBuilder builder, string dataDir)
    {
        builder.Services.AddControllers()
            .AddJsonOptions(opts =>
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
#pragma warning disable MCP9004 // Legacy SSE is intentionally enabled for trusted local compatibility clients.
        builder.Services.AddMcpServer(options =>
        {
            options.ServerInstructions = AgentUpMcpGuidance.ServerInstructions;
        })
            .WithHttpTransport(options =>
            {
                options.Stateless = false;
                options.EnableLegacySse = true;
                options.ConfigureSessionOptions = (context, serverOptions, ct) =>
                    context.RequestServices.GetRequiredService<McpEndpointSessionProvider>()
                        .ConfigureAsync(context, serverOptions, ct);
            })
            .WithTools<OrchestrationMcpTools>()
            .WithTools<CommitQueueMcpTools>()
            .WithTools<BrowserMcpTools>()
            .WithTools<AuditMcpTools>()
            .WithResources<OrchestrationMcpResources>();
#pragma warning restore MCP9004

        builder.Services.AddSingleton<WorkspaceEventBus>();
        builder.Services.AddSingleton<WorkspaceEventFrameProvider>();
        builder.Services.AddSingleton<WorkspaceEventStreamService>();
        builder.Services.AddSingleton<IWorkspaceRepository>(_ =>
            new JsonWorkspaceRepository(Path.Join(dataDir, "workspaces.json")));
        builder.Services.AddSingleton<IOutputRepository>(_ =>
            new FileOutputRepository(dataDir));
        builder.Services.AddSingleton<IAuditEventRepository>(_ =>
            new FileAuditEventRepository(dataDir));
        builder.Services.AddSingleton<IAuditArtifactRepository>(_ =>
            new FileAuditArtifactRepository(dataDir));
        builder.Services.AddSingleton<AuditWorkdirIdProvider>();
        builder.Services.AddSingleton<AuditGitStateProvider>();
        builder.Services.AddSingleton<IAuditIdentityProvider, AuditIdentityProvider>();
        builder.Services.AddSingleton<AuditService>();
        builder.Services.AddSingleton<AuditController>();
        builder.Services.AddHostedService<WorkspaceAuditSubscriber>();
        builder.Services.AddSingleton<IPortRangeStore>(_ =>
            new FilePortRangeStore(Path.Join(dataDir, "port-ranges.json")));
        builder.Services.AddSingleton<IPortAvailabilityProvider, SocketPortAvailabilityProvider>();
        builder.Services.AddSingleton<IPortAllocationService>(sp =>
            new PortAllocationService(
                sp.GetRequiredService<IPortRangeStore>(),
                sp.GetRequiredService<IPortAvailabilityProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PortAllocationService>>()));
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<IDotnetVersionProvider, DotnetVersionProvider>();
        builder.Services.AddSingleton<IDockerVersionProvider, DockerVersionProvider>();
        builder.Services.AddSingleton<ICapabilityAdapter, DotnetCapabilityAdapter>();
        builder.Services.AddSingleton<ICapabilityAdapter, DockerCapabilityAdapter>();
        builder.Services.AddSingleton<CapabilityReconciliationService>();
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<ConsoleSecretRedactor>();
        builder.Services.AddSingleton<ILocalProcessProvider, LocalProcessProvider>();
        builder.Services.AddSingleton<IDockerProcessProvider, DockerProcessProvider>();
        builder.Services.AddSingleton<ProcessOutputService>();
        builder.Services.AddSingleton<ProcessesController>();
        builder.Services.AddSingleton<WorkspaceStateController>();
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<WorkspaceProcessManager>();
        builder.Services.AddSingleton<IWorkspaceProcessManager>(sp => sp.GetRequiredService<WorkspaceProcessManager>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceProcessManager>());
        builder.Services.AddSingleton<WorkspaceLifecycleService>();
        builder.Services.AddSingleton<ApplicationLifecycleService>();
        builder.Services.AddSingleton<IAgentUpConfigurationProvider, AgentUpConfigurationProvider>();
        builder.Services.AddSingleton<IWorkspaceIdentityProvider, GitWorkspaceIdentityProvider>();
        builder.Services.AddSingleton<IAgentUpContextProvider, AgentUpContextProvider>();
        builder.Services.AddSingleton<OrchestrationContextService>();
        builder.Services.AddSingleton<OrchestrationWorkspaceService>();
        builder.Services.AddSingleton<OrchestrationConsoleService>();
        builder.Services.AddSingleton<OrchestrationWorkspaceController>();
        builder.Services.AddSingleton<OrchestrationContextController>();
        builder.Services.AddSingleton<OrchestrationConsoleController>();
        builder.Services.AddSingleton<McpEndpointSessionProvider>();
        builder.Services.AddSingleton<CommitPolicyProvider>();
        builder.Services.AddSingleton<ICommitsGitProvider, CommitsGitProvider>();
        builder.Services.AddSingleton<ICommitsQueueProvider, CommitsQueueProvider>();
        builder.Services.AddSingleton<CommitsService>();
        builder.Services.AddSingleton<CommitsController>();
        builder.Services.AddSingleton<CommitQueueMcpService>();
        builder.Services.AddSingleton<IProcessExitCode, ProcessExitCode>();
        builder.Services.AddSingleton<BrowserSessionStore>();
        builder.Services.AddSingleton<BrowserMcpService>();
        builder.Services.AddSingleton<BrowserEventBus>();
        builder.Services.AddSingleton<BrowserRemoteDisplayService>();
        builder.Services.AddSingleton<IBrowserRemoteSessionProvider, IronRdpBrowserRemoteSessionProvider>();
        builder.Services.AddSingleton<BrowserRemoteSessionService>();
        builder.Services.AddSingleton<BrowserInputDispatcher>();
        builder.Services.AddSingleton<AppHealthCheckService>();
        builder.Services.AddSingleton<AppHealthController>();
        builder.Services.AddSingleton(sp => new WorkspaceStreamStateService(
            sp.GetRequiredService<BrowserEventBus>(),
            sp.GetRequiredService<AppHealthController>(),
            sp.GetRequiredService<WorkspaceQueryController>(),
            sp.GetRequiredService<AuditController>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WorkspaceStreamStateService>>()));
        builder.Services.AddSingleton<WorkspaceStreamStateController>();
        builder.Services.AddHostedService<StreamStateWorkspaceSubscriber>();
        builder.Services.AddSingleton(sp =>
            new HeadlessBrowserSessionManager(
                chromiumDir: Path.Join(dataDir, "chromium"),
                profilesDir: Path.Join(dataDir, "browser-profiles"),
                sp.GetRequiredService<BrowserRemoteDisplayService>(),
                sp.GetRequiredService<WorkspaceStreamStateService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HeadlessBrowserSessionManager>>(),
                configuredExecutablePath: sp.GetRequiredService<IConfiguration>()["Browser:ExecutablePath"]));
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<HeadlessBrowserSessionManager>());
        builder.Services.AddSingleton<CdpBrowserExecutor>();
        builder.Services.AddSingleton<BrowserLifecycleController>();
        builder.Services.AddSingleton<HeadlessBrowserCommandDispatcher>();
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<HeadlessBrowserCommandDispatcher>());
        builder.Services.AddSingleton(sp =>
            new HeadlessBrowserSessionAccessor(sp.GetRequiredService<HeadlessBrowserSessionManager>()));
        builder.Services.AddSingleton<TrayHeartbeatMonitor>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<TrayHeartbeatMonitor>());
    }
}
