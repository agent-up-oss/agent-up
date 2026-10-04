using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AgentUp.Server;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Tests.Features.Authentication.HTTP;

[TestFixture]
public sealed class McpWorkspaceBindingHttpTests
{
    private const string SigningKey = "unit-test-signing-key-32-bytes!!";

    [Test]
    public async Task BoundToken_CanCallItsOwnWorkspaceAndIsRefusedForAnother()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (boundId, otherId) = await RegisterWorkspacesAsync(client);
        var session = await InitializeAsync(client, "/mcp/browser", boundId);

        var own = await CallToolAsync(client, "/mcp/browser", session, "browser_inspect", new { workspaceId = boundId });
        var injected = await CallToolAsync(client, "/mcp/browser", session, "browser_inspect", new { });
        var other = await CallToolAsync(client, "/mcp/browser", session, "browser_inspect", new { workspaceId = otherId });

        Assert.Multiple(() =>
        {
            Assert.That(IsBindingRefusal(own), Is.False);
            Assert.That(IsBindingRefusal(injected), Is.False);
            Assert.That(IsBindingRefusal(other), Is.True);
        });
    }

    [Test]
    public async Task BoundToken_RefusesAWorktreePathFromAnotherWorkspace()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (boundId, _) = await RegisterWorkspacesAsync(client);
        var session = await InitializeAsync(client, "/mcp/commits", boundId);

        var own = await CallToolAsync(client, "/mcp/commits", session, "guard_commits", new { worktreePath = ServerDomain.WorktreePath });
        var other = await CallToolAsync(client, "/mcp/commits", session, "guard_commits", new { worktreePath = ServerDomain.SecondWorktreePath });

        Assert.Multiple(() =>
        {
            Assert.That(IsBindingRefusal(own), Is.False);
            Assert.That(IsBindingRefusal(other), Is.True);
        });
    }

    [Test]
    public async Task BoundSession_OmitsWorkspaceParametersFromAdvertisedTools()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (boundId, _) = await RegisterWorkspacesAsync(client);
        var boundSession = await InitializeAsync(client, "/mcp/browser", boundId);
        var unboundSession = await InitializeAsync(client, "/mcp/browser", workspace: null);
        var boundOrchestration = await InitializeAsync(client, "/mcp/orchestration", boundId);
        var unboundOrchestration = await InitializeAsync(client, "/mcp/orchestration", workspace: null);

        var boundTools = await ListToolsAsync(client, "/mcp/browser", boundSession);
        var unboundTools = await ListToolsAsync(client, "/mcp/browser", unboundSession);
        var boundOrchestrationTools = await ListToolsAsync(client, "/mcp/orchestration", boundOrchestration);
        var unboundOrchestrationTools = await ListToolsAsync(client, "/mcp/orchestration", unboundOrchestration);

        Assert.Multiple(() =>
        {
            Assert.That(HasWorkspaceIdParameter(boundTools, "browser_inspect"), Is.False);
            Assert.That(HasWorkspaceIdParameter(unboundTools, "browser_inspect"), Is.True);
            Assert.That(HasParameter(boundOrchestrationTools, "get_workspace_status", "id"), Is.False);
            Assert.That(HasParameter(unboundOrchestrationTools, "get_workspace_status", "id"), Is.True);
        });
    }

    [Test]
    public async Task BoundToken_RefusesAnotherWorkspaceIdOnOrchestrationTools()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (boundId, otherId) = await RegisterWorkspacesAsync(client);
        var session = await InitializeAsync(client, "/mcp/orchestration", boundId);

        var own = await CallToolAsync(client, "/mcp/orchestration", session, "get_workspace_status", new { id = boundId });
        var other = await CallToolAsync(client, "/mcp/orchestration", session, "get_workspace_status", new { id = otherId });
        var listed = await CallToolAsync(client, "/mcp/orchestration", session, "list_workspaces", new { });
        var started = await CallToolAsync(
            client,
            "/mcp/orchestration",
            session,
            "start_workspace",
            new { worktreePath = ServerDomain.SecondWorktreePath });

        Assert.Multiple(() =>
        {
            Assert.That(IsBindingRefusal(own), Is.False);
            Assert.That(IsBindingRefusal(other), Is.True);
            Assert.That(listed.GetRawText(), Does.Not.Contain(otherId));
            Assert.That(IsBindingRefusal(started), Is.True);
        });
    }

    [Test]
    public async Task UnboundToken_CanStillNameAnyWorkspaceOnMcpTools()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, otherId) = await RegisterWorkspacesAsync(client);
        var session = await InitializeAsync(client, "/mcp/browser", workspace: null);

        var result = await CallToolAsync(client, "/mcp/browser", session, "browser_inspect", new { workspaceId = otherId });

        Assert.That(IsBindingRefusal(result), Is.False);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-mcp-bind-{Guid.NewGuid():N}");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
    }

    private static async Task<(string BoundId, string OtherId)> RegisterWorkspacesAsync(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IssueToken(permissions: OperationPermissions.All));
        var bound = await (await client.PostAsJsonAsync("/api/workspaces", ServerDomain.Workspace().Build()))
            .Content.ReadFromJsonAsync<Workspace>();
        var other = await (await client.PostAsJsonAsync("/api/workspaces", ServerDomain.SecondWorkspace().Build()))
            .Content.ReadFromJsonAsync<Workspace>();
        return (bound!.Id, other!.Id);
    }

    private static async Task<string> InitializeAsync(HttpClient client, string path, string? workspace)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IssueToken(workspace: workspace, permissions: OperationPermissions.All));
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
                    clientInfo = new { name = "binding-test", version = "1" }
                }
            })
        };
        AddMcpHeaders(request);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Mcp-Session-Id").Single();
    }

    private static async Task<JsonElement> CallToolAsync(
        HttpClient client,
        string path,
        string session,
        string name,
        object arguments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new { name, arguments }
            })
        };
        request.Headers.Add("Mcp-Session-Id", session);
        AddMcpHeaders(request);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.IsSuccessStatusCode, Is.True, body);
        return ParseMcpJson(body);
    }

    private static async Task<JsonElement> ListToolsAsync(HttpClient client, string path, string session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "tools/list",
                @params = new { }
            })
        };
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

    private static bool IsBindingRefusal(JsonElement payload)
    {
        var text = payload.GetRawText();
        return text.Contains("cannot target", StringComparison.Ordinal)
               && text.Contains("bound to workspace", StringComparison.Ordinal);
    }

    private static bool HasWorkspaceIdParameter(JsonElement payload, string toolName)
        => HasParameter(payload, toolName, "workspaceId");

    private static bool HasParameter(JsonElement payload, string toolName, string parameter)
    {
        var tools = payload.GetProperty("result").GetProperty("tools");
        var tool = tools.EnumerateArray().Single(item => item.GetProperty("name").GetString() == toolName);
        return tool.GetProperty("inputSchema").GetProperty("properties").TryGetProperty(parameter, out _);
    }

    private static string IssueToken(string? workspace = null, IReadOnlyList<string>? permissions = null)
    {
        var claims = new List<Claim> { new("sub", "user-1") };
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
