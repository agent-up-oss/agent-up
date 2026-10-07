using System.Net;
using System.Net.Http.Json;
using AgentUp.Server;
using AgentUp.Server.Features.Connection.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Connection.HTTP;

[TestFixture]
public sealed class ConnectionHttpTests
{
    [Test]
    public async Task Connection_IsAnonymous()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/connection");
        var body = await response.Content.ReadFromJsonAsync<ConnectionMetadataDto>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body!.Kind, Is.EqualTo("selfHosted"));
            Assert.That(body.Authentication.Mode, Is.EqualTo("disabled"));
        });
    }

    [Test]
    public async Task Connection_DoesNotRequireABearerToken()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.That((await client.GetAsync("/api/connection")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await client.GetAsync("/api/workspaces")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Connection_ReportsLocalAdministratorWhenPasswordIsConfigured()
    {
        using var factory = CreateFactory(("AGENTUP_ADMIN_PASSWORD", "test-password"));
        using var client = factory.CreateClient();
        var body = await client.GetFromJsonAsync<ConnectionMetadataDto>("/api/connection");
        var workspaces = await client.GetAsync("/api/workspaces");
        Assert.Multiple(() =>
        {
            Assert.That(body!.Authentication.Mode, Is.EqualTo("localAdministrator"));
            Assert.That(workspaces.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(params (string Key, string Value)[] values)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AGENTUP_ADMIN_PASSWORD"] = "",
            ["AGENTUP_AUTH_MODE"] = "",
            ["AGENTUP_AUTH_DISABLED"] = ""
        };
        foreach (var (key, value) in values)
            settings[key] = value;

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings)));
    }
}
