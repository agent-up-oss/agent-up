using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Services;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Tests.Features.Agents.HTTP;

[TestFixture]
public sealed class AgentModelProxyHttpTests
{
    [Test]
    public async Task Proxy_forwardsOpencodeWithTheStoredTokenNotTheSandboxKey()
    {
        await using var origin = await Origin.StartAsync();
        var settings = new CapabilityModuleHarness.MemorySettings();
        settings.Write("opencode", new Dictionary<string, string>
        {
            ["endpoint"] = origin.Url,
            ["token"] = "sk-real"
        });
        var access = new AgentModelProxyAccess();
        await using var app = await StartProxyAsync(access, settings);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/agent-model/{access.Key}/opencode/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.Key);
        request.Content = new StringContent("""{"model":"qwen"}""", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var denied = await client.PostAsync($"/api/agent-model/deadbeef/opencode/v1/chat/completions", new StringContent("{}"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Is.EqualTo("{\"ok\":true}"));
            Assert.That(origin.Authorization, Is.EqualTo("Bearer sk-real"));
            Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task Proxy_answersNotFoundForAnUnknownCapability()
    {
        var access = new AgentModelProxyAccess();
        await using var app = await StartProxyAsync(access, new CapabilityModuleHarness.MemorySettings());
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        using var response = await client.GetAsync($"/api/agent-model/{access.Key}/missing/v1/models");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static async Task<WebApplication> StartProxyAsync(
        AgentModelProxyAccess access,
        CapabilityModuleHarness.MemorySettings settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls=http://127.0.0.1:0"],
            EnvironmentName = Environments.Development
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(AgentModelProxyHttpController).Assembly);
        builder.Services.AddSingleton(access);
        builder.Services.AddSingleton<IAgentModelProxyDestinations>(_ =>
        {
            var modules = CapabilityModuleHarness.CreateService(
                enabled: true,
                modules: [new OpencodeAgentCapability()],
                nixAvailable: false,
                settings: settings);
            return new AgentModelProxyDestinations(
                new CapabilityModulesController(modules),
                new ConfigurationBuilder().Build());
        });
        builder.Services.AddSingleton<AgentModelProxyForwarder>();
        builder.Services.AddSingleton<AgentModelProxyService>();
        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class Origin : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private Origin(WebApplication app) => _app = app;

        public string Url => _app.Urls.Single();
        public string? Authorization { get; private set; }

        public static async Task<Origin> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = ["--urls=http://127.0.0.1:0"],
                EnvironmentName = Environments.Development
            });
            var app = builder.Build();
            var origin = new Origin(app);
            app.MapPost("/v1/chat/completions", async context =>
            {
                origin.Authorization = context.Request.Headers.Authorization.ToString();
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"ok\":true}");
            });
            await app.StartAsync();
            return origin;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
