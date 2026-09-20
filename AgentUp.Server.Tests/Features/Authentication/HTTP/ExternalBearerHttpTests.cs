using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using AgentUp.Server;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Tests.Features.Authentication.HTTP;

[TestFixture]
public sealed class ExternalBearerHttpTests
{
    private const string SigningKey = "unit-test-signing-key-32-bytes!!";

    [Test]
    public async Task RestRoutes_AcceptExternalBearerTokensWithWorkspaceBinding()
    {
        using var root = new WebApplicationFactory<Program>();
        using var factory = CreateFactory(root);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IssueToken(
                tenant: "ten-1",
                workspace: "ws-a",
                permissions: [OperationPermissions.WorkspaceRead]));

        var list = await client.GetAsync("/api/workspaces");
        var forbidden = await client.GetAsync("/api/workspaces/ws-b");
        var matching = await client.GetAsync("/api/workspaces/ws-a");

        Assert.Multiple(() =>
        {
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(matching.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task WebSocketRoutes_AcceptTheAuthenticationSubprotocol()
    {
        using var root = new WebApplicationFactory<Program>();
        using var factory = CreateFactory(root);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(IssueToken(
                workspace: "ws-a",
                permissions: [OperationPermissions.BrowserControl])))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var wsClient = factory.Server.CreateWebSocketClient();
        wsClient.ConfigureRequest = request =>
            request.Headers["Sec-WebSocket-Protocol"] = $"{WebSocketAuthenticationProtocol.Prefix}{encoded}";

        using var socket = await wsClient.ConnectAsync(new Uri("ws://localhost/api/browser/rdp/ws-a"), CancellationToken.None);
        Assert.That(socket.State, Is.EqualTo(WebSocketState.Open));
    }

    [Test]
    public async Task RestRoutes_ForbidExternalBearerTokensWithoutTheOperationPermission()
    {
        using var root = new WebApplicationFactory<Program>();
        using var factory = CreateFactory(root);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IssueToken(workspace: "ws-a"));

        var list = await client.GetAsync("/api/workspaces");
        var entitlements = await client.GetAsync("/api/entitlements");

        Assert.Multiple(() =>
        {
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(entitlements.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(WebApplicationFactory<Program> root)
        => root.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AGENTUP_AUTH_MODE"] = "externalBearer",
                    ["AGENTUP_EXTERNAL_ISSUER"] = "https://issuer.test",
                    ["AGENTUP_EXTERNAL_AUDIENCE"] = "environment-1",
                    ["AGENTUP_EXTERNAL_SIGNING_KEY"] = SigningKey
                })));

    private static string IssueToken(
        string? tenant = null,
        string? workspace = null,
        IReadOnlyList<string>? permissions = null)
    {
        var claims = new List<Claim> { new("sub", "user-1") };
        if (tenant is not null)
            claims.Add(new Claim("tenant", tenant));
        if (workspace is not null)
            claims.Add(new Claim("workspace", workspace));
        foreach (var permission in permissions ?? [])
            claims.Add(new Claim("permissions", permission));

        var token = new JwtSecurityToken(
            "https://issuer.test",
            "environment-1",
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
