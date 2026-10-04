using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using AgentUp.InstallerConfig;
using AgentUp.Server.Composition;
using AgentUp.Server.Features.ApplicationProxy.Controllers;
using AgentUp.Server.Features.Authentication.Controllers;
using AgentUp.Server.Features.Authentication.Interfaces;
using Microsoft.AspNetCore.HttpOverrides;

RepositoryDotEnv.LoadOptional();

var builder = WebApplication.CreateBuilder(args);
SentryTelemetry.Configure(builder);

builder.Host.UseSystemd();
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "Agent-Up Server";
});

ServiceRegistration.Configure(builder, ResolveDataDirectory());

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSwagger();
app.UseSwaggerUI();
app.UseWebSockets();
app.UseCors(AgentUp.Server.Shared.Providers.WebClientOriginProvider.PolicyName);
app.UseAuthentication();
// The MCP network restriction runs after authentication so an opted-in remote caller has
// already been identified when the 404-or-continue decision is made.
app.UseMiddleware<IMcpNetworkRestrictionMiddleware>();
app.UseAuthorization();
app.UseMiddleware<IWorkspaceBindingMiddleware>();
app.MapControllers();
McpEndpointMapping.Map(app, app.Services.GetRequiredService<IMcpRemoteAccess>().IsEnabled);
app.MapFallback("{**path}", (HttpContext context, ApplicationProxyFallbackController proxy) =>
    proxy.ForwardFallback(context)).AllowAnonymous();

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
