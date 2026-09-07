using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentUp.Server;
using AgentUp.Server.Features.Authentication.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.HTTP;

[TestFixture]
public class AuthenticationHttpTests
{
    [Test]
    public async Task RestRoutes_RequireLogin_AndAcceptIssuedToken()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AGENTUP_ADMIN_PASSWORD"] = "test-password" })));
        using var client = factory.CreateClient();

        Assert.That((await client.GetAsync("/api/workspaces")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test-password"));
        var credentials = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials!.AccessToken);
        var authorized = await client.GetAsync("/api/workspaces");

        Assert.Multiple(() =>
        {
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(authorized.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task RestRoutes_RequireLogin_EvenWhenAdminPasswordIsNotConfigured()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<LoginResponse>("/api/auth/status");
        var workspaces = await client.GetAsync("/api/workspaces");

        Assert.Multiple(() =>
        {
            Assert.That(status!.AuthenticationRequired, Is.True);
            Assert.That(workspaces.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task AuthenticationCanBeDisabled()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AGENTUP_AUTH_DISABLED"] = "true" })));
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<LoginResponse>("/api/auth/status");
        var workspaces = await client.GetAsync("/api/workspaces");

        Assert.Multiple(() =>
        {
            Assert.That(status!.AuthenticationRequired, Is.False);
            Assert.That(workspaces.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }
}
