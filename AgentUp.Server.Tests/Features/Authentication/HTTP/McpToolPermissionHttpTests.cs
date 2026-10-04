using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AgentUp.Server;
using AgentUp.Server.Features.Authentication.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Tests.Features.Authentication.HTTP;

/// <summary>
/// Proves the advertised toolset and the call boundary agree: a tool a token cannot use is both
/// absent from tools/list and refused when its name is sent anyway.
/// </summary>
[TestFixture]
public sealed class McpToolPermissionHttpTests
{
    private const string SigningKey = "unit-test-signing-key-32-bytes!!";

    [Test]
    public async Task ReadOnlyToken_SeesOnlyTheReadToolsOfTheEndpoint()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await InitializeAsync(client, "/mcp/commits", [OperationPermissions.GitRead]);

        var tools = await ToolNamesAsync(client, "/mcp/commits", session);

        Assert.Multiple(() =>
        {
            Assert.That(tools, Does.Contain("get_commits_status"));
            Assert.That(tools, Does.Contain("guard_commits"));
            Assert.That(tools, Does.Not.Contain("enqueue_commit"));
            Assert.That(tools, Does.Not.Contain("clear_commits"));
        });
    }

    [Test]
    public async Task WriteToken_SeesTheMutatingToolsAsWell()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await InitializeAsync(
            client,
            "/mcp/commits",
            [OperationPermissions.GitRead, OperationPermissions.GitWrite]);

        var tools = await ToolNamesAsync(client, "/mcp/commits", session);

        Assert.Multiple(() =>
        {
            Assert.That(tools, Does.Contain("get_commits_status"));
            Assert.That(tools, Does.Contain("enqueue_commit"));
        });
    }

    /// <summary>
    /// The per-endpoint allowlist and the permission filter compose; neither widens the other. A
    /// token holding every permission still sees only the slice the endpoint serves.
    /// </summary>
    [Test]
    public async Task FullyPermittedToken_StillSeesOnlyTheEndpointsOwnSlice()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await InitializeAsync(client, "/mcp/orchestration", OperationPermissions.All);

        var tools = await ToolNamesAsync(client, "/mcp/orchestration", session);

        Assert.Multiple(() =>
        {
            Assert.That(tools, Does.Contain("start_workspace"));
            Assert.That(tools, Does.Contain("list_workspaces"));
            Assert.That(tools, Does.Not.Contain("enqueue_commit"));
            Assert.That(tools, Does.Not.Contain("browser_inspect"));
        });
    }

    [Test]
    public async Task ReadOnlyToken_LosesTheWorkspaceStartAndStopToolsOnOrchestration()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await InitializeAsync(client, "/mcp/orchestration", [OperationPermissions.WorkspaceRead]);

        var tools = await ToolNamesAsync(client, "/mcp/orchestration", session);

        Assert.Multiple(() =>
        {
            Assert.That(tools, Does.Contain("list_workspaces"));
            Assert.That(tools, Does.Contain("get_workspace_status"));
            Assert.That(tools, Does.Not.Contain("start_workspace"));
            Assert.That(tools, Does.Not.Contain("stop_workspace"));
            Assert.That(tools, Does.Not.Contain("get_workspace_diagnostics"));
        });
    }

    [Test]
    public async Task UnscopedToken_KeepsTodaysFullEndpointToolset()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await InitializeAsync(client, "/mcp/orchestration", []);

        var tools = await ToolNamesAsync(client, "/mcp/orchestration", session);

        Assert.Multiple(() =>
        {
            Assert.That(tools, Does.Contain("start_workspace"));
            Assert.That(tools, Does.Contain("stop_workspace"));
            Assert.That(tools, Does.Contain("get_workspace_diagnostics"));
        });
    }

    [Test]
    public async Task ReadOnlyToken_IsRefusedWhenItCallsAToolNameItRemembers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await InitializeAsync(client, "/mcp/orchestration", [OperationPermissions.WorkspaceRead]);

        var allowed = await CallToolAsync(client, "/mcp/orchestration", session, "list_workspaces", new { });
        var refused = await CallToolAsync(
            client,
            "/mcp/orchestration",
            session,
            "start_workspace",
            new { worktreePath = "/repos/app" });

        Assert.Multiple(() =>
        {
            Assert.That(ResultText(allowed), Does.Not.Contain("Missing permission"));
            Assert.That(
                ResultText(refused),
                Does.Contain("Missing permission 'workspace.start' for tool 'start_workspace'."));
        });
    }

    private static string ResultText(JsonElement payload)
        => string.Concat(payload.GetProperty("result").GetProperty("content")
            .EnumerateArray()
            .Select(block => block.TryGetProperty("text", out var text) ? text.GetString() : null));

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-mcp-perm-{Guid.NewGuid():N}");
        WebApplicationFactory<Program>? root = null;
        try
        {
            root = new WebApplicationFactory<Program>();
            var configured = root.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Storage:DataDirectory", dataDirectory);
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["AGENTUP_AUTH_MODE"] = "externalBearer",
                        ["AGENTUP_EXTERNAL_ISSUER"] = "https://issuer.test",
                        ["AGENTUP_EXTERNAL_AUDIENCE"] = "environment-1",
                        ["AGENTUP_EXTERNAL_SIGNING_KEY"] = SigningKey
                    }));
            });
            root = null;
            return configured;
        }
        finally
        {
            root?.Dispose();
        }
    }

    private static async Task<string> InitializeAsync(HttpClient client, string path, IReadOnlyList<string> permissions)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IssueToken(permissions));
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-06-18",
                    capabilities = new { },
                    clientInfo = new { name = "permission-test", version = "1" }
                }
            })
        };
        AddMcpHeaders(request);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Mcp-Session-Id").Single();
    }

    private static async Task<string[]> ToolNamesAsync(HttpClient client, string path, string session)
    {
        var payload = await SendAsync(client, path, session, new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/list",
            @params = new { }
        });

        return payload.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .ToArray();
    }

    private static Task<JsonElement> CallToolAsync(
        HttpClient client,
        string path,
        string session,
        string name,
        object arguments)
        => SendAsync(client, path, session, new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new { name, arguments }
        });

    private static async Task<JsonElement> SendAsync(HttpClient client, string path, string session, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Mcp-Session-Id", session);
        AddMcpHeaders(request);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.IsSuccessStatusCode, Is.True, body);
        return ParseMcpJson(body);
    }

    private static void AddMcpHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
    }

    private static JsonElement ParseMcpJson(string body)
    {
        var json = body.Trim();
        if (json.Contains("\ndata:", StringComparison.Ordinal) || json.StartsWith("event:", StringComparison.Ordinal))
        {
            var data = json.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .First(line => line.StartsWith("data:", StringComparison.Ordinal));
            json = data["data:".Length..].Trim();
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string IssueToken(IReadOnlyList<string> permissions)
    {
        var claims = new List<Claim> { new("sub", "user-1") };
        claims.AddRange(permissions.Select(permission => new Claim("permissions", permission)));

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
