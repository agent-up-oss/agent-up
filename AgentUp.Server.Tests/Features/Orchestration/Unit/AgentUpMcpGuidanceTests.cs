using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Providers;

namespace AgentUp.Server.Tests.Features.Orchestration.Unit;

/// <summary>
/// Server instructions are a product contract: they are the only thing telling a client whether
/// it may address a workspace by path. These assertions are about the addressing rule each
/// audience reads, not about prose.
/// </summary>
[TestFixture]
public sealed class AgentUpMcpGuidanceTests
{
    [Test]
    public void SharedFilesystem_KeepsThePathFirstStartAdvice()
    {
        var instructions = AgentUpMcpGuidance.ForSharedFilesystem();

        Assert.Multiple(() =>
        {
            Assert.That(instructions, Does.Contain("call start_workspace with its absolute path immediately"));
            Assert.That(instructions, Does.Contain("Do not call list_workspaces or get_workspace_status before start_workspace"));
            Assert.That(instructions, Does.Contain("This Server runs on the host you are on"));
        });
    }

    [Test]
    public void Remote_TellsTheCallerToSelectAWorkspaceByIdInsteadOfByPath()
    {
        var instructions = AgentUpMcpGuidance.ForRemote();

        Assert.Multiple(() =>
        {
            Assert.That(instructions, Does.Contain("You do not share a filesystem with this Server"));
            Assert.That(instructions, Does.Contain("call list_workspaces"));
            Assert.That(instructions, Does.Contain("pass its id to start_workspace"));
            Assert.That(instructions, Does.Not.Contain("call start_workspace with its absolute path"));
        });
    }

    [Test]
    public void Remote_DoesNotCallTheServersEnvironmentLocal()
    {
        var instructions = AgentUpMcpGuidance.ForRemote();

        Assert.Multiple(() =>
        {
            Assert.That(instructions, Does.Not.Contain("local development environment"));
            Assert.That(instructions, Does.Not.Contain("local AI-assisted development"));
            Assert.That(instructions, Does.Contain("the development environment this Server hosts"));
        });
    }

    [Test]
    public void Pinned_NamesTheWorkspaceAndRemovesTheSelectionStep()
    {
        var instructions = AgentUpMcpGuidance.ForPinned("ws-a");

        Assert.Multiple(() =>
        {
            Assert.That(instructions, Does.Contain("This session is pinned to workspace ws-a"));
            Assert.That(instructions, Does.Contain("There is no workspace to choose"));
            Assert.That(instructions, Does.Contain("do not call list_workspaces to find one"));
        });
    }

    [Test]
    public void Pinned_FallsBackToAnUnnamedWorkspaceWhenTheClaimIsBlank()
    {
        var instructions = AgentUpMcpGuidance.ForPinned(" ");

        Assert.That(instructions, Does.Contain("pinned to workspace a single workspace"));
    }

    [TestCase(McpInstructionAudience.SharedFilesystem)]
    [TestCase(McpInstructionAudience.Remote)]
    [TestCase(McpInstructionAudience.Pinned)]
    public void EveryAudience_KeepsTheValidationLoopAndCommitQueueDiscipline(McpInstructionAudience audience)
    {
        var instructions = AgentUpMcpGuidance.Build(new McpInstructionContext(audience, "ws-a"));

        Assert.Multiple(() =>
        {
            Assert.That(instructions, Does.Contain("Agent-Up validation is a feedback loop"));
            Assert.That(instructions, Does.Contain("inspect the workspace console immediately"));
            Assert.That(instructions, Does.Contain("call guard_commits"));
            Assert.That(instructions, Does.Contain("use enqueue_commit to declare each logical vertical-slice commit"));
            Assert.That(instructions, Does.Contain("Do not run git add, git commit, or git stash directly"));
        });
    }

    [Test]
    public void ForEndpoint_KeepsTheSliceTextAndAppendsTheSameAddressingRule()
    {
        var endpoint = AgentUpMcpGuidance.ForEndpoint("Agent-Up commit queue MCP server.", McpInstructionContext.Remote);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint, Does.StartWith("Agent-Up commit queue MCP server."));
            Assert.That(endpoint, Does.Contain(AgentUpMcpGuidance.Addressing(McpInstructionContext.Remote)));
            Assert.That(endpoint, Does.Not.Contain("Agent-Up validation is a feedback loop"));
        });
    }

    [Test]
    public void ServerInstructions_DefaultToTheSharedFilesystemVariant()
        => Assert.That(AgentUpMcpGuidance.ServerInstructions, Is.EqualTo(AgentUpMcpGuidance.ForSharedFilesystem()));
}
