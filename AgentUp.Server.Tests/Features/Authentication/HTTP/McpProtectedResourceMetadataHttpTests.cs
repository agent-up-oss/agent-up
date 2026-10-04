using System.Net;
using System.Text.Json;
using AgentUp.Server.Features.Authentication.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Authentication.HTTP;

/// <summary>
/// RFC 9728 protected resource metadata. Agent-Up is the resource server, so the document
/// points at the configured external issuer and never at Agent-Up itself, and it only exists
/// once remote MCP access is on.
/// </summary>
/// <remarks>
/// The absence cases assert that nothing publishes metadata rather than a specific status:
/// with the handler unregistered the path reaches the application proxy fallback, whose
/// refusal code depends on the connection rather than on MCP.
/// </remarks>
[TestFixture]
public sealed class McpProtectedResourceMetadataHttpTests
{
    private const string Issuer = "https://issuer.test";
    private const string MetadataPath = "/.well-known/oauth-protected-resource";

    [Test]
    public async Task MetadataNamesTheConfiguredIssuerAsTheAuthorizationServer()
    {
        using var factory = CreateFactory(remoteEnabled: true, issuer: Issuer);
        using var client = factory.CreateClient();

        var document = await ReadMetadataAsync(client, $"{MetadataPath}/mcp/browser");

        Assert.Multiple(() =>
        {
            Assert.That(Strings(document, "authorization_servers"), Is.EqualTo(new[] { $"{Issuer}/" }));
            Assert.That(Strings(document, "bearer_methods_supported"), Is.EqualTo(new[] { "header" }));
            Assert.That(Strings(document, "scopes_supported"), Does.Contain(OperationPermissions.BrowserControl));
        });
    }

    [Test]
    public async Task MetadataDerivesTheResourceIdentifierFromTheRequest()
    {
        using var factory = CreateFactory(remoteEnabled: true, issuer: Issuer);
        using var client = factory.CreateClient();

        var document = await ReadMetadataAsync(client, $"{MetadataPath}/mcp/browser");

        Assert.That(document.GetProperty("resource").GetString(), Does.EndWith("/mcp/browser"));
    }

    [Test]
    public async Task NoMetadataIsPublishedWhenRemoteMcpAccessIsOff()
    {
        using var factory = CreateFactory(remoteEnabled: false, issuer: Issuer);
        using var client = factory.CreateClient();

        var (status, body) = await GetAsync(client, $"{MetadataPath}/mcp/browser");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.Not.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Not.Contain("authorization_servers"));
        });
    }

    [Test]
    public async Task NoMetadataIsPublishedWhenNoIssuerNamesAnAuthorizationServer()
    {
        using var factory = CreateFactory(remoteEnabled: true, issuer: null);
        using var client = factory.CreateClient();

        var (status, body) = await GetAsync(client, $"{MetadataPath}/mcp/browser");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.Not.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Not.Contain("authorization_servers"));
        });
    }

    private static async Task<(HttpStatusCode Status, string Body)> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> ReadMetadataAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static string[] Strings(JsonElement document, string property)
        => document.GetProperty(property)
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();

    private static WebApplicationFactory<Program> CreateFactory(bool remoteEnabled, string? issuer)
    {
        var dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-mcp-prm-{Guid.NewGuid():N}");
        WebApplicationFactory<Program>? root = null;
        try
        {
            root = new WebApplicationFactory<Program>();
            var configured = root.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Storage:DataDirectory", dataDirectory);
                builder.UseSetting("AGENTUP_AUTH_MODE", issuer is null ? null : "externalBearer");
                builder.UseSetting("AGENTUP_EXTERNAL_ISSUER", issuer);
                builder.UseSetting("AGENTUP_EXTERNAL_AUDIENCE", issuer is null ? null : "environment-1");
                builder.UseSetting(
                    "AGENTUP_EXTERNAL_SIGNING_KEY",
                    issuer is null ? null : "unit-test-signing-key-32-bytes!!");
                builder.UseSetting("AGENTUP_MCP_REMOTE_ENABLED", remoteEnabled ? "true" : null);
            });
            root = null;
            return configured;
        }
        finally
        {
            root?.Dispose();
        }
    }
}
