using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Text.Json;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.Agents.HTTP;

[TestFixture]
public sealed class AgentsHttpTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        var port = FreePort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [$"--urls=http://127.0.0.1:{port}"] });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Codex:Command"] = "/definitely-missing/codex-acp"
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(AgentsHttpController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton(_ => new CapabilityReconciliationService([]));
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<WorkspaceEventBus>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<AgentCommandProvider>();
        builder.Services.AddSingleton<IAgentProcessFactory, AgentProcessFactory>();
        builder.Services.AddSingleton<AgentEventFrameProvider>();
        builder.Services.AddSingleton<AgentEventService>();
        builder.Services.AddSingleton<AgentSchedulingService>();
        builder.Services.AddSingleton<AgentsController>();
        _app = builder.Build();
        _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task Get_unknownWorkspaceReturnsNotFound()
    {
        using var response = await _client.GetAsync("/api/workspaces/missing/agent");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Get_registeredWorkspaceReturnsIdleSessionAndAvailability()
    {
        var workspace = await RegisterAsync();
        var session = await _client.GetFromJsonAsync<AgentSessionDto>($"/api/workspaces/{workspace.Id}/agent", JsonOptions);
        Assert.Multiple(() => { Assert.That(session!.State, Is.EqualTo("idle")); Assert.That(session.Agent, Is.Null); Assert.That(session.Agents, Has.Count.EqualTo(3)); });
    }

    [Test]
    public async Task Schedule_unavailableAdapterReturnsStructuredConflict()
    {
        var workspace = await RegisterAsync();
        using var response = await _client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/agent", new { agent = "Codex" });
        Assert.Multiple(() => { Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict)); Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json")); });
    }

    [Test]
    public async Task Prompt_emptyMessageReturnsValidationProblem()
    {
        var workspace = await RegisterAsync();
        using var response = await _client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/agent/messages", new { message = "  " });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Authenticate_withoutScheduledAgentReturnsNotFound()
    {
        var workspace = await RegisterAsync();
        using var response = await _client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/agent/authenticate", new { methodId = "chatgpt" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PermissionDecision_requiresBothIdentifiers()
    {
        var workspace = await RegisterAsync();
        using var response = await _client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/agent/permissions", new { requestId = "", optionId = "" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private Task<Workspace> RegisterAsync() => _app.Services.GetRequiredService<WorkspaceQueryController>().RegisterAsync(
        new RegisterWorkspaceRequest("Workspace", "/repo", "/repo", "main", "abc"));

    private static int FreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
