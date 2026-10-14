using System.Runtime.CompilerServices;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Agents.Controller;

[TestFixture]
public sealed class RoleAllowlistControllerTests
{
    [Test]
    public async Task RestrictRoleTools_hidesAndRefusesToolsOutsideTheWorkspaceAllowlist()
    {
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService()),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(
            ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var workspaces = new WorkspaceQueryController(registry);
        var autonomy = new AutonomousWorkspaceService(
            new ConfiguredAutonomySettings(new ConfigurationBuilder().AddInMemoryCollection().Build(), settings),
            settings,
            new LoopPreconditionProvider(
                workspaces,
                new AgentUp.Server.Features.Git.Controllers.GitQueryController(
                    new AgentUp.Server.Features.Git.Services.GitChangeTreeService(
                        workspaces, new FakeGitWorkingTreeProvider(), new FakeWorkspacePromptGuard())),
                new AgentUp.Server.Features.Commits.Controllers.CommitsController(
                    new AgentUp.Server.Features.Commits.Services.CommitsService(
                        new EmptyCommitsQueue(),
                        new EmptyCommitsGit(),
                        new AgentUp.CommitPolicy.Features.CommitPolicy.Providers.CommitPolicyProvider())),
                settings,
                new AlwaysSupportedInspector()),
            new MemoryAssignedIssueQueue(),
            new MemoryAssignedProposalQueue(),
            new MemoryRoleProgress(),
            null!,
            null!,
            workspaces,
            new RoleToolFilterProvider());
        await autonomy.SetRoleAsync(
            workspace.Id,
            new AgentRoleDefinition("Review", ["read_file"], RoleTriggerKind.ProposalAwaitingReview, RoleTerminationKind.ReviewSubmitted));
        var options = OptionsWith("read_file", "write_file");

        autonomy.RestrictRoleTools(options, workspace.Id);
        var filter = options.Filters.Request.CallToolFilters[^1];
        var refused = await InvokeAsync(filter, "write_file");

        Assert.Multiple(() =>
        {
            Assert.That(options.ToolCollection!.PrimitiveNames, Is.EquivalentTo(new[] { "read_file" }));
            Assert.That(refused.IsError, Is.True);
        });
    }

    private static McpServerOptions OptionsWith(params string[] toolNames)
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var name in toolNames)
            collection.Add(McpServerTool.Create(() => "ok", new McpServerToolCreateOptions { Name = name }));
        return new McpServerOptions { ToolCollection = collection };
    }

    private static async Task<CallToolResult> InvokeAsync(
        McpRequestFilter<CallToolRequestParams, CallToolResult> filter,
        string name)
    {
        var handler = filter((_, _) => ValueTask.FromResult(new CallToolResult { IsError = false }));
        var context = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(
            typeof(RequestContext<CallToolRequestParams>));
        typeof(RequestContext<CallToolRequestParams>).GetProperty(nameof(RequestContext<CallToolRequestParams>.Params))!
            .SetValue(context, new CallToolRequestParams { Name = name });
        return await handler(context, CancellationToken.None);
    }
}
