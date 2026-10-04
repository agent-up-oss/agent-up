using System.Net;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Shared.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Orchestration.Unit;

[TestFixture]
public sealed class McpSessionInstructionTests
{
    [Test]
    public void InstructionContext_PinsASessionWhoseTokenCarriesAWorkspaceClaim()
    {
        var selected = McpEndpointSessionProvider.InstructionContext(RequestFrom(IPAddress.Parse("10.1.2.3")), "ws-a");

        Assert.Multiple(() =>
        {
            Assert.That(selected.Audience, Is.EqualTo(McpInstructionAudience.Pinned));
            Assert.That(selected.WorkspaceId, Is.EqualTo("ws-a"));
        });
    }

    [TestCase("127.0.0.1")]
    [TestCase("::1")]
    public void InstructionContext_TreatsALoopbackCallerAsSharingTheServersFilesystem(string remote)
    {
        var selected = McpEndpointSessionProvider.InstructionContext(RequestFrom(IPAddress.Parse(remote)), boundWorkspace: null);

        Assert.That(selected.Audience, Is.EqualTo(McpInstructionAudience.SharedFilesystem));
    }

    [Test]
    public void InstructionContext_TreatsAnUnknownPeerAsSharingTheServersFilesystem()
    {
        var selected = McpEndpointSessionProvider.InstructionContext(RequestFrom(null), boundWorkspace: null);

        Assert.That(selected.Audience, Is.EqualTo(McpInstructionAudience.SharedFilesystem));
    }

    [Test]
    public void InstructionContext_TreatsANonLoopbackCallerAsRemote()
    {
        var selected = McpEndpointSessionProvider.InstructionContext(
            RequestFrom(IPAddress.Parse("10.1.2.3")),
            boundWorkspace: null);

        Assert.Multiple(() =>
        {
            Assert.That(selected.Audience, Is.EqualTo(McpInstructionAudience.Remote));
            Assert.That(selected.WorkspaceId, Is.Null);
        });
    }

    [Test]
    public async Task ConfigureAsync_GivesALoopbackOrchestrationSessionThePathFirstInstructions()
    {
        var options = new McpServerOptions();

        await new McpEndpointSessionProvider().ConfigureAsync(
            RequestFor("/mcp/orchestration", IPAddress.Loopback),
            options,
            CancellationToken.None);

        Assert.That(options.ServerInstructions, Is.EqualTo(AgentUpMcpGuidance.ForSharedFilesystem()));
    }

    [Test]
    public async Task ConfigureAsync_GivesARemoteOrchestrationSessionTheWorkspaceIdInstructions()
    {
        var options = new McpServerOptions();

        await new McpEndpointSessionProvider().ConfigureAsync(
            RequestFor("/mcp/orchestration", IPAddress.Parse("10.1.2.3")),
            options,
            CancellationToken.None);

        Assert.That(options.ServerInstructions, Is.EqualTo(AgentUpMcpGuidance.ForRemote()));
    }

    /// <summary>
    /// A slice endpoint still has to answer the filesystem question, so its own text is extended
    /// with the addressing rule rather than replacing it.
    /// </summary>
    [Test]
    public async Task ConfigureAsync_AppendsTheAddressingRuleToASliceEndpoint()
    {
        var options = new McpServerOptions();

        await new McpEndpointSessionProvider().ConfigureAsync(
            RequestFor("/mcp/commits", IPAddress.Parse("10.1.2.3")),
            options,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(options.ServerInstructions, Does.Contain("commit queue MCP server"));
            Assert.That(options.ServerInstructions, Does.Contain(AgentUpMcpGuidance.Addressing(McpInstructionContext.Remote)));
        });
    }

    [Test]
    public async Task ConfigureAsync_FallsBackToTheRootInstructionsForAnUnrecognizedPath()
    {
        var options = new McpServerOptions();

        await new McpEndpointSessionProvider().ConfigureAsync(
            RequestFor("/mcp/unknown", IPAddress.Loopback),
            options,
            CancellationToken.None);

        Assert.That(options.ServerInstructions, Is.EqualTo(AgentUpMcpGuidance.ServerInstructions));
    }

    private static HttpContext RequestFrom(IPAddress? remote)
        => RequestFor("/mcp/orchestration", remote);

    private static HttpContext RequestFor(string path, IPAddress? remote)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = remote;
        return context;
    }
}
