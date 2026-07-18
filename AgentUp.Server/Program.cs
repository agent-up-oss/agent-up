using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Interfaces;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Interfaces;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Ports.Providers;
using AgentUp.Server.Features.Ports.Services;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Providers;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Mcp.Controllers;
using AgentUp.Server.Features.Mcp.Interfaces;
using AgentUp.Server.Features.Mcp.Providers;
using AgentUp.Server.Features.Mcp.Services;
using AgentUp.Server.Features.Mcp.Tools;
using AgentUp.Server.Features.Mcp.Resources;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSystemd();
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "Agent-Up Server";
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#pragma warning disable MCP9004 // Legacy SSE is intentionally enabled for trusted local compatibility clients.
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = false;
        options.EnableLegacySse = true;
    })
    .WithTools<AgentUpMcpTools>()
    .WithResources<AgentUpMcpResources>();
#pragma warning restore MCP9004

var dataDir = ResolveDataDirectory();
builder.Services.AddSingleton<IWorkspaceRepository>(_ =>
    new JsonWorkspaceRepository(Path.Join(dataDir, "workspaces.json")));
builder.Services.AddSingleton<IOutputRepository>(_ =>
    new FileOutputRepository(dataDir));
builder.Services.AddSingleton<IPortRangeStore>(_ =>
    new FilePortRangeStore(Path.Join(dataDir, "port-ranges.json")));
builder.Services.AddSingleton<IPortAvailabilityProvider, SocketPortAvailabilityProvider>();
builder.Services.AddSingleton<IPortAllocationService>(sp =>
    new PortAllocationService(
        sp.GetRequiredService<IPortRangeStore>(),
        sp.GetRequiredService<IPortAvailabilityProvider>(),
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PortAllocationService>>()));
builder.Services.AddSingleton<WorkspaceRegistry>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
builder.Services.AddSingleton<IDotnetVersionProvider, DotnetVersionProvider>();
builder.Services.AddSingleton<IDockerVersionProvider, DockerVersionProvider>();
builder.Services.AddSingleton<ICapabilityAdapter, DotnetCapabilityAdapter>();
builder.Services.AddSingleton<ICapabilityAdapter, DockerCapabilityAdapter>();
builder.Services.AddSingleton<CapabilityReconciliationService>();
builder.Services.AddSingleton<ILocalProcessProvider, LocalProcessProvider>();
builder.Services.AddSingleton<IDockerProcessProvider, DockerProcessProvider>();
builder.Services.AddSingleton<WorkspaceProcessManager>();
builder.Services.AddSingleton<IWorkspaceProcessManager>(sp => sp.GetRequiredService<WorkspaceProcessManager>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceProcessManager>());
builder.Services.AddSingleton<IAgentUpConfigurationProvider, AgentUpConfigurationProvider>();
builder.Services.AddSingleton<IWorkspaceIdentityProvider, GitWorkspaceIdentityProvider>();
builder.Services.AddSingleton<IAgentUpContextProvider, AgentUpContextProvider>();
builder.Services.AddSingleton<McpWorkspaceService>();
builder.Services.AddSingleton<McpWorkspaceController>();
builder.Services.AddSingleton<McpContextController>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapMcp("/mcp");

app.Run();

string ResolveDataDirectory()
{
    var configured = builder.Configuration["Storage:DataDirectory"];
    if (!string.IsNullOrWhiteSpace(configured))
        return configured;

    if (builder.Environment.IsDevelopment())
    {
        var checkoutId = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(AppContext.BaseDirectory)))[..16];
        return Path.Join(Path.GetTempPath(), "AgentUp", checkoutId);
    }

    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (!string.IsNullOrWhiteSpace(localAppData))
        return Path.Join(localAppData, "AgentUp");

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return Path.Join("/Library", "Application Support", "Agent-Up");

    return Path.Join("/var", "lib", "agent-up");
}

namespace AgentUp.Server
{
    public partial class Program;
}
