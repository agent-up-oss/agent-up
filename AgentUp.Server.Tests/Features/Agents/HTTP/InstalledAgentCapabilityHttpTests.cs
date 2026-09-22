using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.Agents.HTTP;

[TestFixture]
[CancelAfter(60_000)]
[Timeout(60_000)]
public sealed class InstalledAgentCapabilityHttpTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = ["--urls=http://127.0.0.1:0"] });
        builder.Services.AddControllers().AddApplicationPart(typeof(AgentsHttpController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton(_ => new CapabilityReconciliationService());
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<WorkspaceEventBus>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<IEnabledCapabilityPackages>(_ => FakeEnabledCapabilityPackages.FirstParty());
        builder.Services.AddSingleton<AgentCommandProvider>();
        builder.Services.AddSingleton<AgentSubscriptionAuth>();
        builder.Services.AddSingleton(_ => new AgentCliHomeProvider(Path.Join(Path.GetTempPath(), "agent-up-http-agents")));
        builder.Services.AddSingleton<IAgentClaudeCredentialStore, AgentClaudeCredentialStore>();
        builder.Services.AddSingleton<IAgentProcessEnvironmentProvider, AgentProcessEnvironmentProvider>();
        builder.Services.AddSingleton<AgentLoginCommandProvider>();
        builder.Services.AddSingleton<AgentLoginFlowProvider>();
        builder.Services.AddHttpClient("agent-login-callback");
        builder.Services.AddSingleton<AgentLoginCallbackRelay>();
        builder.Services.AddSingleton<IAgentSubscriptionLoginProvider, AgentSubscriptionLoginProvider>();
        builder.Services.AddSingleton<IAgentProcessFactory, AgentProcessFactory>();
        builder.Services.AddSingleton<AgentEventFrameProvider>();
        builder.Services.AddSingleton<AgentEventService>();
        builder.Services.AddSingleton<IAgentSessionRepository, InMemoryAgentSessionRepository>();
        builder.Services.AddSingleton<AgentSchedulingService>();
        builder.Services.AddSingleton<AgentsController>();
        _app = builder.Build();
        _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single().TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(45) };
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task Get_reportsAvailabilityMatchingEnabledAgentPackages()
    {
        var workspace = await RegisterAsync();
        using var response = await _client.GetAsync($"/api/workspaces/{workspace.Id}/agent");
        var session = await response.Content.ReadFromJsonAsync<AgentSessionDto>(JsonOptions);
        var commands = _app.Services.GetRequiredService<AgentCommandProvider>();
        var packages = _app.Services.GetRequiredService<IEnabledCapabilityPackages>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.Agents, Has.Count.EqualTo(3));

        foreach (var descriptor in session.Agents)
        {
            var plan = packages.AgentLaunch(descriptor.Agent);
            var available = await commands.IsAvailableAsync(descriptor.Agent, TestContext.CurrentContext.CancellationToken);
            var command = await commands.ResolveAsync(descriptor.Agent, TestContext.CurrentContext.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(descriptor.DisplayName, Is.Not.WhiteSpace);
                Assert.That(descriptor.Available, Is.EqualTo(plan is not null));
                Assert.That(descriptor.Available, Is.EqualTo(available));
                Assert.That(command is not null, Is.EqualTo(descriptor.Available));
                if (command is null)
                    return;

                Assert.That(command.FileName, Is.Not.WhiteSpace);
                Assert.That(command.Arguments, Is.Not.Null);
            });
        }
    }

    private Task<Workspace> RegisterAsync() => _app.Services.GetRequiredService<WorkspaceQueryController>().RegisterAsync(
        ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
}
