using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AutonomyPreconditionsTests
{
    [Test]
    public void Refusal_namesTheOutstandingStepAndItsRemedy()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AutonomyPreconditions.Refusal(AutonomyPreconditions.ForgeCapabilityEnabled),
                Is.EqualTo("forgeCapabilityEnabled: Run: agent-up capabilities bootstrap"));
            Assert.That(
                AutonomyPreconditions.Refusal(AutonomyPreconditions.HostConnectionAuthorized),
                Is.EqualTo("hostConnectionAuthorized: Sign in to your repository host."));
            Assert.That(
                AutonomyPreconditions.Refusal(AutonomyPreconditions.AgentAvailable),
                Is.EqualTo("agentAvailable: Enable an agent capability"));
            Assert.That(
                AutonomyPreconditions.Refusal(AutonomyPreconditions.AutonomyEnabled),
                Is.EqualTo("autonomyEnabled: Enable autonomy for this Server"));
        });
    }

    [Test]
    public void Remedy_returnsTheIdWhenTheStepIsUnknown()
    {
        Assert.That(AutonomyPreconditions.Remedy("unknownStep"), Is.EqualTo("unknownStep"));
    }

    [Test]
    public void Remedy_namesTheControlSurfaceSteps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.ForgeCapabilityEnabled), Is.EqualTo(AutonomyPreconditions.ForgeCapabilityRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.HostConnectionAuthorized), Is.EqualTo(AutonomyPreconditions.HostConnectionRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.AgentAvailable), Is.EqualTo(AutonomyPreconditions.AgentAvailableRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.AutonomyEnabled), Is.EqualTo(AutonomyPreconditions.AutonomyEnabledRemedy));
        });
    }

    [Test]
    public void Remedy_namesTheLoopActivationSteps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.WorkspaceRegistered), Is.EqualTo(AutonomyPreconditions.WorkspaceRegisteredRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.BoundAgent), Is.EqualTo(AutonomyPreconditions.BoundAgentRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.PermissionRequests), Is.EqualTo(AutonomyPreconditions.PermissionRequestsRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.PermissionPolicy), Is.EqualTo(AutonomyPreconditions.PermissionPolicyRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.GitBranch), Is.EqualTo(AutonomyPreconditions.GitBranchRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.UnpushedCommits), Is.EqualTo(AutonomyPreconditions.UnpushedCommitsRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.CommitQueueEdit), Is.EqualTo(AutonomyPreconditions.CommitQueueEditRemedy));
            Assert.That(AutonomyPreconditions.Remedy(AutonomyPreconditions.UnassignedChanges), Is.EqualTo(AutonomyPreconditions.UnassignedChangesRemedy));
        });
    }

    [Test]
    public void Refusal_usesTheStepIdWhenTheRemedyFallsBack()
    {
        Assert.That(AutonomyPreconditions.Refusal("unknownStep"), Is.EqualTo("unknownStep: unknownStep"));
    }
}
