using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Authentication.Controllers;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Authentication.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class InProcessAgentToolServiceTests
{
    [Test]
    public async Task ListAsync_offersAPmOnlyBrowserAndApplicationToolsPlusCompleteTask()
    {
        var (service, _) = Arrange(TeamMemberKind.PmReviewer);

        var names = (await service.ListAsync("pm-1", CancellationToken.None)).Select(tool => tool.Name).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("browser_click").And.Contain("browser_screenshot").And.Contain("get_workspace_status"));
            Assert.That(names, Does.Not.Contain("get_workspace_diagnostics"));
            Assert.That(names, Does.Not.Contain("list_workspaces"));
            Assert.That(names, Does.Contain(InProcessAgentToolService.CompleteTask));
            // No forge on this Server, so there is nothing to submit a review to.
            Assert.That(names, Does.Not.Contain(InProcessAgentToolService.SubmitReview));
        });
    }

    [Test]
    public async Task ListAsync_aDevReviewerLosesTheBrowser()
    {
        var (service, _) = Arrange(TeamMemberKind.DevReviewer);

        var names = (await service.ListAsync("pm-1", CancellationToken.None)).Select(tool => tool.Name).ToArray();

        Assert.That(names, Does.Not.Contain("browser_click").And.Contain("get_workspace_diagnostics"));
    }

    [Test]
    public async Task CallAsync_pinsTheWorkspaceTheModelCannotChoose()
    {
        var (service, mcp) = Arrange(TeamMemberKind.PmReviewer);

        await service.CallAsync("pm-1", "browser_click",
            JsonSerializer.SerializeToElement(new { workspaceId = "author-1", selector = "#save" }),
            CancellationToken.None);

        var call = mcp.Calls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(call.Endpoint, Is.EqualTo("/mcp/browser"));
            Assert.That(call.Arguments["workspaceId"].GetString(), Is.EqualTo("pm-1"));
            Assert.That(call.Arguments["selector"].GetString(), Is.EqualTo("#save"));
        });
    }

    [Test]
    public async Task CallAsync_refusesAToolOutsideTheGrantWithoutCallingMcp()
    {
        var (service, mcp) = Arrange(TeamMemberKind.PmReviewer);

        var result = await service.CallAsync("pm-1", "get_workspace_diagnostics",
            JsonSerializer.SerializeToElement(new { }), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.Text, Does.Contain("PmReviewer"));
            Assert.That(mcp.Calls, Is.Empty);
        });
    }

    [Test]
    public async Task CallAsync_submitReviewIsRefusedWithoutAForge()
    {
        var (service, _) = Arrange(TeamMemberKind.PmReviewer);

        var result = await service.CallAsync("pm-1", InProcessAgentToolService.SubmitReview,
            JsonSerializer.SerializeToElement(new { proposalNumber = "7", body = "broken" }), CancellationToken.None);

        Assert.That(result.IsError, Is.True);
    }

    private static (InProcessAgentToolService Service, FakeMcp Mcp) Arrange(TeamMemberKind member)
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(JsonAgentWorkspaceSettingsRepository.Empty("pm-1") with { Member = member });
        var members = new TeamMemberService(settings, new WorkspaceQueryController(ServerTestComposition.CreateRegistry()));
        var permissions = new McpToolPermissionController(new McpToolPermissionService(new McpToolPermissionFilterProvider()));
        var mcp = new FakeMcp();
        return (new InProcessAgentToolService(mcp, new InProcessToolCatalogProvider(), members, permissions, new MemoryRoleProgress()), mcp);
    }

    private sealed class FakeMcp : ILoopbackMcpClient
    {
        public List<(string Endpoint, string Tool, IReadOnlyDictionary<string, JsonElement> Arguments)> Calls { get; } = [];

        public Task<IReadOnlyList<JsonElement>> ListToolsAsync(string endpoint, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<JsonElement>>(endpoint switch
            {
                "/mcp/browser" => [Pinned("browser_click"), Pinned("browser_screenshot")],
                "/mcp/orchestration" => [Pinned("get_workspace_status"), Pinned("get_workspace_diagnostics"), Unpinned("list_workspaces")],
                _ => []
            });

        public Task<JsonElement> CallToolAsync(
            string endpoint,
            string tool,
            IReadOnlyDictionary<string, JsonElement> arguments,
            CancellationToken cancellationToken)
        {
            Calls.Add((endpoint, tool, arguments));
            return Task.FromResult(JsonSerializer.SerializeToElement(new { content = new[] { new { type = "text", text = "ok" } } }));
        }

        private static JsonElement Pinned(string name) => JsonSerializer.SerializeToElement(new
        {
            name,
            inputSchema = new { type = "object", properties = new { workspaceId = new { type = "string" } }, required = new[] { "workspaceId" } }
        });

        private static JsonElement Unpinned(string name) => JsonSerializer.SerializeToElement(new
        {
            name,
            inputSchema = new { type = "object", properties = new { } }
        });
    }
}
