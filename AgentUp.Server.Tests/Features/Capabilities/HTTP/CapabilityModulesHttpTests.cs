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
    public async Task List_returns_the_built_in_capabilities()
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

        using var enable = await client.PostAsJsonAsync("/api/capabilities/enable", new EnableCapabilityModuleRequest("dotnet"));
        var enabled = await enable.Content.ReadFromJsonAsync<CapabilityModuleDto>(Json);
        Assert.That(enabled!.Enabled, Is.True);

        using var disable = await client.PostAsync("/api/capabilities/disable/dotnet", null);
        var disabled = await disable.Content.ReadFromJsonAsync<CapabilityModuleDto>(Json);
        Assert.That(disabled!.Enabled, Is.False);
    }

    [Test]
    public async Task List_names_nix_when_an_enabled_module_cannot_run()
    {
        await using var host = await StartAsync(nixAvailable: false, enabled: true);
        using var client = host.Client;

        using var response = await client.GetAsync("/api/capabilities");
        var modules = await response.Content.ReadFromJsonAsync<List<CapabilityModuleDto>>(Json);
        var listed = modules!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(listed.State, Is.EqualTo("unavailable"));
            Assert.That(listed.CanRun, Is.False);
            Assert.That(listed.Messages, Has.Some.Contains("https://nixos.org/download"));
        });
    }

    [Test]
    public async Task Enable_is_409_while_the_capability_software_is_missing()
    {
        await using var host = await StartAsync(nixAvailable: false);

        using var response = await host.Client.PostAsJsonAsync("/api/capabilities/enable", new EnableCapabilityModuleRequest("dotnet"));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(problem.GetProperty("detail").GetString(), Does.Contain("Nix"));
    }

    [Test]
    public async Task Enable_is_404_for_a_capability_the_server_was_not_built_with()
    {
        await using var host = await StartAsync();

        using var response = await host.Client.PostAsJsonAsync("/api/capabilities/enable", new EnableCapabilityModuleRequest("python"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Settings_put_stores_a_secret_without_returning_it()
    {
        await using var host = await StartAsync(enabled: true, modules: [Configurable()]);

        using var response = await host.Client.PutAsJsonAsync(
            "/api/capabilities/opencode/settings",
            new { values = new Dictionary<string, string> { ["endpoint"] = "http://localhost:11434", ["token"] = "sk-secret" } });
        var body = await response.Content.ReadAsStringAsync();
        var module = JsonSerializer.Deserialize<CapabilityModuleDto>(body, Json)!;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body, Does.Not.Contain("sk-secret"));
        Assert.That(module.State, Is.EqualTo("ready"));
        Assert.That(module.Settings.Single(setting => setting.Key == "token").HasValue, Is.True);
    }

    [Test]
    public async Task Settings_put_is_400_for_an_endpoint_that_is_not_an_http_url()
    {
        await using var host = await StartAsync(enabled: true, modules: [Configurable()]);

        using var response = await host.Client.PutAsJsonAsync(
            "/api/capabilities/opencode/settings",
            new { values = new Dictionary<string, string> { ["endpoint"] = "ftp://models.test" } });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private static StubAgentCapability Configurable() => new()
    {
        Identity = new AgentUp.Sdk.Common.CapabilityIdentity("opencode", "1.0.0", "OpenCode", "agent-up"),
        Settings =
        [
            new AgentUp.Sdk.Common.CapabilitySettingSpec("endpoint", "Endpoint", "url", Required: true),
            new AgentUp.Sdk.Common.CapabilitySettingSpec("token", "Token", "secret")
        ]
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<Host> StartAsync(
        bool nixAvailable = true,
        bool enabled = false,
        IReadOnlyList<AgentUp.Sdk.Common.ICapability>? modules = null)
    {
        var port = TcpPort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [$"--urls=http://127.0.0.1:{port}"] });
        builder.Services.AddControllers().AddApplicationPart(typeof(CapabilityModulesHttpController).Assembly);
        builder.Services.AddSingleton(CapabilityModuleHarness.CreateService(nixAvailable, enabled, modules));
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
