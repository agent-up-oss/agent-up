using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AgentUp.Server.Features.Authentication.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Tests.Features.Authentication.HTTP;

/// <summary>
/// Remote MCP access. The default is unchanged - <c>/mcp</c> is a 404 off loopback - and the
/// opt-in mode answers only an authenticated caller holding that endpoint's permission.
/// </summary>
/// <remarks>
/// A non-loopback caller is simulated with <c>X-Forwarded-For</c>: the test connection is
/// loopback, so the forwarded-headers middleware trusts it as a proxy and rewrites the remote
/// address, which is the same thing the Server sees behind a real reverse proxy.
/// </remarks>
[TestFixture]
public sealed class McpRemoteAccessHttpTests
{
    private const string SigningKey = "unit-test-signing-key-32-bytes!!";
    private const string ForeignSigningKey = "another-signing-key-32-bytes!!!!";
    private const string RemoteAddress = "203.0.113.10";
    private const string Issuer = "https://issuer.test";
    private const string Audience = "environment-1";

    [TestCase("/mcp/browser", OperationPermissions.BrowserControl)]
    [TestCase("/mcp/audit", OperationPermissions.DiagnosticsRead)]
    public async Task RemoteMcpIsHiddenByDefaultEvenForAFullyPermittedToken(string path, string permission)
    {
        using var factory = CreateFactory(remoteEnabled: false);
        using var client = factory.CreateClient();

        using var response = await InitializeAsync(client, path, IssueToken([permission]), fromRemote: true);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("/mcp/browser", OperationPermissions.BrowserControl)]
    [TestCase("/mcp/audit", OperationPermissions.DiagnosticsRead)]
    public async Task RemoteMcpAnswersATokenCarryingTheEndpointPermission(string path, string permission)
    {
        using var factory = CreateFactory(remoteEnabled: true);
        using var client = factory.CreateClient();

        using var response = await InitializeAsync(client, path, IssueToken([permission]), fromRemote: true);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.Contains("Mcp-Session-Id"), Is.True);
        });
    }

    [Test]
    public async Task ARemoteSessionCanListAndCallTheEndpointTools()
    {
        using var factory = CreateFactory(remoteEnabled: true);
        using var client = factory.CreateClient();
        var token = IssueToken([OperationPermissions.WorkspaceRead]);
        var session = await OpenSessionAsync(client, "/mcp/orchestration", token);

        var tools = await PostAsync(client, "/mcp/orchestration", token, ToolsListPayload(), session);
        var call = await PostAsync(client, "/mcp/orchestration", token, ToolCallPayload("list_workspaces"), session);

        Assert.Multiple(() =>
        {
            Assert.That(tools, Does.Contain("list_workspaces"));
            Assert.That(call, Does.Contain("\"result\""));
        });
    }

    [Test]
    public async Task RemoteMcpIsHiddenWhenNoTokenIsPresented()
    {
        using var factory = CreateFactory(remoteEnabled: true);
        using var client = factory.CreateClient();

        using var response = await InitializeAsync(client, "/mcp/browser", token: null, fromRemote: true);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task RemoteMcpIsHiddenWhenTheTokenDoesNotVerify()
    {
        using var factory = CreateFactory(remoteEnabled: true);
        using var client = factory.CreateClient();
        var foreign = IssueToken(OperationPermissions.All, signingKey: ForeignSigningKey);

        using var response = await InitializeAsync(client, "/mcp/browser", foreign, fromRemote: true);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task RemoteMcpIsHiddenWhenTheTokenHasExpired()
    {
        using var factory = CreateFactory(remoteEnabled: true);
        using var client = factory.CreateClient();
        var expired = IssueToken(OperationPermissions.All, lifetime: TimeSpan.FromMinutes(-5));

        using var response = await InitializeAsync(client, "/mcp/browser", expired, fromRemote: true);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task RemoteMcpIsHiddenWhenTheTokenNamesAnotherAudience()
    {
        using var factory = CreateFactory(remoteEnabled: true);
        using var client = factory.CreateClient();
        var foreign = IssueToken(OperationPermissions.All, audience: "another-environment");

        using var response = await InitializeAsync(client, "/mcp/browser", foreign, fromRemote: true);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("/mcp/browser", OperationPermissions.DiagnosticsRead)]
    [TestCase("/mcp/commits", OperationPermissions.GitRead)]
    public async Task RemoteMcpForbidsAnAuthenticatedTokenWithoutTheEndpointPermission(
        string path,
        string permission)
    {
        using var factory = CreateFactory(remoteEnabled: true);
        using var client = factory.CreateClient();

        using var response = await InitializeAsync(client, path, IssueToken([permission]), fromRemote: true);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task LoopbackMcpStaysAnonymousWhateverRemoteAccessSays(bool remoteEnabled)
    {
        using var factory = CreateFactory(remoteEnabled);
        using var client = factory.CreateClient();

        using var response = await InitializeAsync(client, "/mcp/browser", token: null, fromRemote: false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static WebApplicationFactory<Program> CreateFactory(bool remoteEnabled)
    {
        var dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-mcp-remote-{Guid.NewGuid():N}");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataDirectory", dataDirectory);
            builder.UseSetting("AGENTUP_AUTH_MODE", "externalBearer");
            builder.UseSetting("AGENTUP_EXTERNAL_ISSUER", Issuer);
            builder.UseSetting("AGENTUP_EXTERNAL_AUDIENCE", Audience);
            builder.UseSetting("AGENTUP_EXTERNAL_SIGNING_KEY", SigningKey);
            builder.UseSetting("AGENTUP_MCP_REMOTE_ENABLED", remoteEnabled ? "true" : null);
        });
    }

    private static async Task<string> OpenSessionAsync(HttpClient client, string path, string token)
    {
        using var response = await InitializeAsync(client, path, token, fromRemote: true);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return response.Headers.GetValues("Mcp-Session-Id").Single();
    }

    private static Task<HttpResponseMessage> InitializeAsync(
        HttpClient client,
        string path,
        string? token,
        bool fromRemote)
        => SendAsync(client, path, token, InitializePayload(), session: null, fromRemote);

    private static async Task<string> PostAsync(
        HttpClient client,
        string path,
        string token,
        object payload,
        string session)
    {
        using var response = await SendAsync(client, path, token, payload, session, fromRemote: true);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.IsSuccessStatusCode, Is.True, body);
        return body;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string path,
        string? token,
        object payload,
        string? session,
        bool fromRemote)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (token is not null)
            request.Headers.Add("Authorization", $"Bearer {token}");
        if (session is not null)
            request.Headers.Add("Mcp-Session-Id", session);
        if (fromRemote)
            request.Headers.Add("X-Forwarded-For", RemoteAddress);

        return await client.SendAsync(request);
    }

    private static object InitializePayload()
        => new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "remote-access-test", version = "1" }
            }
        };

    private static object ToolsListPayload()
        => new { jsonrpc = "2.0", id = 2, method = "tools/list", @params = new { } };

    private static object ToolCallPayload(string name)
        => new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name, arguments = new { } } };

    private static string IssueToken(
        IReadOnlyList<string> permissions,
        string signingKey = SigningKey,
        string audience = Audience,
        TimeSpan? lifetime = null)
    {
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5));
        var claims = permissions
            .Select(permission => new Claim("permissions", permission))
            .Prepend(new Claim("sub", "user-1"));
        var token = new JwtSecurityToken(
            Issuer,
            audience,
            claims,
            notBefore: expires.AddMinutes(-6),
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
