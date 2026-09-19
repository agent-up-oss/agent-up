using System.Net;
using System.Security.Cryptography.X509Certificates;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;
using AgentUp.Server.Features.ApplicationProxy.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeLoopbackHttpPortProbe : ILoopbackHttpPortProbe
{
    public HashSet<int> OpenPorts { get; } = [];
    public bool IsListening(int port) => OpenPorts.Contains(port);
}

internal sealed class FakeApplicationHttpForwarder : IApplicationHttpForwarder
{
    public int? LastPort { get; private set; }
    public string? LastPath { get; private set; }
    public int Calls { get; private set; }

    public Task ForwardAsync(HttpContext context, int allocatedPort)
    {
        Calls++;
        LastPort = allocatedPort;
        LastPath = context.Request.Path.Value;
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }
}

internal sealed class StubTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class StubTlsConnectionFeature : ITlsConnectionFeature
{
    public X509Certificate2? ClientCertificate { get; set; }

    public Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken)
        => Task.FromResult<X509Certificate2?>(null);
}

internal static class ApplicationProxyHarness
{
    public static DefaultHttpContext LoopbackContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        return context;
    }

    public static DefaultHttpContext TlsContext(string host = "agent.example")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        context.Features.Set<ITlsConnectionFeature>(new StubTlsConnectionFeature());
        return context;
    }

    public static async Task<(ApplicationProxyService Service, string WorkspaceId, int Port, FakeLoopbackHttpPortProbe Probe, FakeApplicationHttpForwarder Forwarder, StubTimeProvider Clock, AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyTicketStore Tickets)> CreateAsync(
        string protocol = "http",
        bool portOpen = true)
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .Named("Proxy")
            .WithWorktreePath($"{ServerDomain.RepositoryPath}/{Guid.NewGuid():N}")
            .WithApplication(new ApplicationDefinitionBuilder("web", "echo")
                .WithPort(ServerDomain.Port().Named("WEB_PORT").On(5173).WithProtocol(protocol)))
            .Build());
        var port = workspace.Applications[0].AllocatedPorts[0].AllocatedPort;
        var probe = new FakeLoopbackHttpPortProbe();
        if (portOpen)
            probe.OpenPorts.Add(port);
        var forwarder = new FakeApplicationHttpForwarder();
        var clock = new StubTimeProvider();
        var tickets = new AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyTicketStore(clock);
        var cookies = new AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyCookieProtector(
            new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        var credentials = new AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyCredentials(cookies);
        var service = new ApplicationProxyService(
            new WorkspaceQueryController(registry),
            probe,
            tickets,
            credentials,
            new AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyOriginMapper(),
            new AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyCsrfGuard(),
            forwarder,
            new AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyErrorWriter(),
            new AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyTransportGuard(),
            new AgentUp.Server.Features.ApplicationProxy.Providers.ApplicationProxyBootstrapPage(),
            clock);
        return (service, workspace.Id, port, probe, forwarder, clock, tickets);
    }
}
