using AgentUp.Server.Features.ApplicationProxy.DTOs;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Support;
using AgentUp.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Net.Http;
using System.Net;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace AgentUp.Server.Tests.Features.ApplicationProxy.HTTP;

[TestFixture]
public sealed class ApplicationProxyHttpTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = CreateFactory(new Dictionary<string, string?> { ["AGENTUP_AUTH_DISABLED"] = "true" });
    }

    [TearDown]
    public void TearDown() => _factory.Dispose();

    [Test]
    public async Task Tickets_requireAuthenticationWhenTheServerEnforcesLogin()
    {
        using var factory = CreateFactory(new Dictionary<string, string?> { ["AGENTUP_ADMIN_PASSWORD"] = "test-password" });
        using var client = factory.CreateClient();

        var anonymous = await client.PostAsJsonAsync("/api/apps/tickets", new ApplicationProxyTicketRequest("ws", 10100));
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test-password"));
        var credentials = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials!.AccessToken);
        var authorizedMissing = await client.PostAsJsonAsync("/api/apps/tickets", new ApplicationProxyTicketRequest("missing", 10100));

        Assert.Multiple(() =>
        {
            Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(authorizedMissing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task Proxy_forwardsApplicationHttpThroughAnAuthenticatedTicket()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });
        var workspace = await RegisterHttpWorkspaceAsync(client);
        var port = workspace.Applications[0].AllocatedPorts[0].AllocatedPort;
        await using var backend = await StartBackendAsync(port);

        var ticket = await IssueTicketAsync(client, workspace.Id, port);
        using var bootstrap = await client.SendAsync(TicketRequest(HttpMethod.Get, ticket.BootstrapPath, ticket.Ticket));
        var page = await bootstrap.Content.ReadAsStringAsync();
        var asset = await client.GetStringAsync("/assets/app.js");
        using var ping = new StringContent("ping-body");
        using var echoRequest = new HttpRequestMessage(HttpMethod.Post, "/echo") { Content = ping };
        echoRequest.Headers.TryAddWithoutValidation("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
        using var echo = await client.SendAsync(echoRequest);
        var echoed = await echo.Content.ReadAsStringAsync();
        var authorization = await client.GetStringAsync("/incoming-authorization");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Contain("hello-app"));
            Assert.That(asset, Is.EqualTo("window.APP=1"));
            Assert.That(echoed, Is.EqualTo("ping-body"));
            Assert.That(authorization, Is.Empty);
        });
    }

    [Test]
    public async Task Proxy_setsAnHttpOnlyCookieAndRejectsTicketReuse()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var workspace = await RegisterHttpWorkspaceAsync(client);
        var port = workspace.Applications[0].AllocatedPorts[0].AllocatedPort;
        await using var backend = await StartBackendAsync(port);
        var ticket = await IssueTicketAsync(client, workspace.Id, port);

        using var bootstrap = await client.SendAsync(TicketRequest(HttpMethod.Get, ticket.BootstrapPath, ticket.Ticket));
        using var replay = await client.SendAsync(TicketRequest(HttpMethod.Get, ticket.BootstrapPath, ticket.Ticket));

        Assert.Multiple(() =>
        {
            Assert.That(bootstrap.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(bootstrap.Headers.Location!.ToString(), Is.EqualTo("/"));
            Assert.That(string.Join('\n', bootstrap.Headers.GetValues("Set-Cookie")), Does.Contain("agent-up-proxy="));
            Assert.That(string.Join('\n', bootstrap.Headers.GetValues("Set-Cookie")), Does.Contain("httponly").IgnoreCase);
            Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task Proxy_rewritesLoopbackRedirectsAndStripsFrameAncestors()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var workspace = await RegisterHttpWorkspaceAsync(client);
        var port = workspace.Applications[0].AllocatedPorts[0].AllocatedPort;
        await using var backend = await StartBackendAsync(port);
        var ticket = await IssueTicketAsync(client, workspace.Id, port);
        using var bootstrap = await client.SendAsync(TicketRequest(HttpMethod.Get, ticket.BootstrapPath, ticket.Ticket));
        using var home = await client.GetAsync(bootstrap.Headers.Location);
        using var redirect = await client.GetAsync("/go");
        using var framed = await client.GetAsync("/framed");

        Assert.That(home.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(redirect.Headers.Location, Is.Not.Null);
        Assert.That(redirect.Headers.Location!.ToString(), Does.Contain("/login"));
        Assert.That(redirect.Headers.Location.ToString(), Does.Not.Contain("127.0.0.1"));
        Assert.That(framed.Headers.Contains("X-Frame-Options"), Is.False);
    }

    [Test]
    public async Task Proxy_rejectsClosedAndNonHttpPortsAndAnonymousFallback()
    {
        using var client = _factory.CreateClient();
        var httpWorkspace = await RegisterHttpWorkspaceAsync(client);
        var tcpWorkspace = await RegisterWorkspaceAsync(client, "tcp", ServerDomain.Port().Named("DB_PORT").On(5432).WithProtocol("tcp").Build());
        var httpPort = httpWorkspace.Applications[0].AllocatedPorts[0].AllocatedPort;
        var tcpPort = tcpWorkspace.Applications[0].AllocatedPorts[0].AllocatedPort;

        var closed = await client.PostAsJsonAsync("/api/apps/tickets", new ApplicationProxyTicketRequest(httpWorkspace.Id, httpPort));
        var tcp = await client.PostAsJsonAsync("/api/apps/tickets", new ApplicationProxyTicketRequest(tcpWorkspace.Id, tcpPort));
        using var fallback = await client.GetAsync("/");

        Assert.Multiple(() =>
        {
            Assert.That(closed.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(tcp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(fallback.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task Proxy_rejectsQueryStringTickets()
    {
        using var factory = CreateFactory(new Dictionary<string, string?> { ["AGENTUP_ADMIN_PASSWORD"] = "test-password" });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test-password"));
        var credentials = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials!.AccessToken);
        var workspace = await RegisterHttpWorkspaceAsync(client);
        var port = workspace.Applications[0].AllocatedPorts[0].AllocatedPort;
        await using var backend = await StartBackendAsync(port);
        var ticket = await IssueTicketAsync(client, workspace.Id, port);
        client.DefaultRequestHeaders.Authorization = null;

        using var query = await client.GetAsync($"{ticket.BootstrapPath}?ticket={ticket.Ticket}");
        using var header = await client.SendAsync(TicketRequest(HttpMethod.Get, ticket.BootstrapPath, ticket.Ticket));

        Assert.Multiple(() =>
        {
            Assert.That(query.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(query.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/html"));
            Assert.That(query.Headers.Contains("Set-Cookie"), Is.False);
            Assert.That(header.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(header.Headers.Location!.ToString(), Is.EqualTo("/"));
        });
    }

    private static HttpRequestMessage TicketRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Agent-Up-Ticket", ticket);
        return request;
    }

    private static async Task<Workspace> RegisterHttpWorkspaceAsync(HttpClient client)
        => await RegisterWorkspaceAsync(client, "http", ServerDomain.Port().Named("WEB_PORT").On(5173).Build());

    private static async Task<Workspace> RegisterWorkspaceAsync(HttpClient client, string name, PortDeclaration port)
    {
        var worktree = $"/tmp/agent-up-proxy-{name}-{Guid.NewGuid():N}";
        using var response = await client.PostAsJsonAsync("/api/workspaces", ServerDomain.Workspace()
            .Named($"proxy-{name}")
            .At(worktree)
            .WithApplication(new ApplicationDefinitionBuilder(name, "echo").WithPort(port).Build())
            .Build());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;
    }

    private static async Task<ApplicationProxyTicketResponse> IssueTicketAsync(HttpClient client, string workspaceId, int port)
    {
        using var response = await client.PostAsJsonAsync("/api/apps/tickets", new ApplicationProxyTicketRequest(workspaceId, port));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationProxyTicketResponse>())!;
    }

    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?> settings)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, LoopbackRemoteAddressStartupFilter>());
        });

    private static async Task<WebApplication> StartBackendAsync(int port)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [$"--urls=http://127.0.0.1:{port}"]
        });
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var app = builder.Build();
        app.MapGet("/", () => Results.Content("<html><body>hello-app</body></html>", "text/html"));
        app.MapGet("/assets/app.js", () => Results.Text("window.APP=1", "text/javascript"));
        app.MapPost("/echo", async (Microsoft.AspNetCore.Http.HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            return Results.Text(await reader.ReadToEndAsync());
        });
        app.MapGet("/incoming-authorization", (Microsoft.AspNetCore.Http.HttpRequest request) => request.Headers.Authorization.ToString());
        app.MapGet("/go", () => Results.Redirect($"http://127.0.0.1:{port}/login"));
        app.MapGet("/login", () => Results.Text("login"));
        app.MapGet("/framed", (Microsoft.AspNetCore.Http.HttpResponse response) =>
        {
            response.Headers["X-Frame-Options"] = "DENY";
            return Results.Text("ok");
        });
        await app.StartAsync();
        return app;
    }
}

file sealed class LoopbackRemoteAddressStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                context.Connection.LocalIpAddress ??= IPAddress.Loopback;
                await nextMiddleware();
            });
            next(app);
        };
}
