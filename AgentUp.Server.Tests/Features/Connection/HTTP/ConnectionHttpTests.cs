using System.Net;
using System.Net.Http.Json;
using AgentUp.Server;
using AgentUp.Server.Features.Connection.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Connection.HTTP;

[TestFixture]
public sealed class ConnectionHttpTests
{
    [Test]
    public async Task Connection_IsAnonymous()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/connection");
        var body = await response.Content.ReadFromJsonAsync<ConnectionMetadataDto>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body!.Kind, Is.EqualTo("selfHosted"));
        });
    }

    [Test]
    public async Task Connection_DoesNotRequireABearerToken()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.That((await client.GetAsync("/api/connection")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await client.GetAsync("/api/workspaces")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
