using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.Capabilities.HTTP;

[TestFixture]
public sealed class CapabilityModulesHttpTests
{
    [Test]
    public async Task List_returns_local_registry_modules()
    {
        await using var host = await StartAsync();
        using var client = host.Client;

        using var response = await client.GetAsync("/api/capabilities");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var modules = await response.Content.ReadFromJsonAsync<List<CapabilityModuleDto>>(Json);
        Assert.That(modules!.Single().Id, Is.EqualTo("dotnet"));
    }

    [Test]
    public async Task Enable_then_disable_changes_the_module_state()
    {
        await using var host = await StartAsync();
        using var client = host.Client;

        using var enable = await client.PostAsJsonAsync("/api/capabilities/enable", new EnableCapabilityModuleRequest("dotnet", "1.0.0"));
        var enabled = await enable.Content.ReadFromJsonAsync<CapabilityModuleDto>(Json);
        Assert.That(enabled!.Enabled, Is.True);

        using var disable = await client.PostAsync("/api/capabilities/disable/dotnet", null);
        var disabled = await disable.Content.ReadFromJsonAsync<CapabilityModuleDto>(Json);
        Assert.That(disabled!.Enabled, Is.False);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<Host> StartAsync()
    {
        var port = TcpPort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [$"--urls=http://127.0.0.1:{port}"] });
        builder.Services.AddControllers().AddApplicationPart(typeof(CapabilityModulesHttpController).Assembly);
        builder.Services.AddSingleton(CapabilityModuleHarness.CreateService());
        builder.Services.AddSingleton<CapabilityModulesController>();
        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return new Host(app, new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") });
    }

    private static int TcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class Host(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
