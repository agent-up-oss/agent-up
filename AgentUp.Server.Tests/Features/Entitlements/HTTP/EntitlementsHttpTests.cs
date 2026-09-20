using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentUp.Server;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Entitlements.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Entitlements.HTTP;

[TestFixture]
public sealed class EntitlementsHttpTests
{
    [Test]
    public async Task Entitlements_RequireAuthentication()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.That((await client.GetAsync("/api/entitlements")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Entitlements_ReturnCommunityFeaturesAfterLogin()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AGENTUP_ADMIN_PASSWORD"] = "test-password" })));
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test-password"));
        var credentials = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials!.AccessToken);
        var entitlements = await client.GetFromJsonAsync<EntitlementsDto>("/api/entitlements");

        Assert.Multiple(() =>
        {
            Assert.That(entitlements!.Source, Is.EqualTo("selfHosted"));
            Assert.That(entitlements.Features["agent.prompt"].Available, Is.True);
        });
    }
}
