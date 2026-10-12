using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class LoopInvariantGuardTests
{
    [Test]
    public void Evaluate_acceptsACleanCompletedRun()
    {
        var violations = new LoopInvariantGuard().Evaluate(new LoopRunResultBuilder().Build());

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void Evaluate_failsWhenAPermissionIsLeftOnTheQueue()
    {
        var violations = new LoopInvariantGuard().Evaluate(new LoopRunResultBuilder().PermissionsHeld().Build());

        Assert.That(violations, Has.Some.Contain("permission request"));
    }

    [Test]
    public void Evaluate_failsWhenThePromptGateIsStillHeld()
    {
        var violations = new LoopInvariantGuard().Evaluate(new LoopRunResultBuilder().GateHeld().Build());

        Assert.That(violations, Has.Some.Contain("prompt gate"));
    }

    [Test]
    public void Evaluate_failsWhenTheTranscriptExceedsTheBound()
    {
        var violations = new LoopInvariantGuard().Evaluate(
            new LoopRunResultBuilder().Transcript(LoopInvariantGuard.MaxTranscriptEvents + 1).Build());

        Assert.That(violations, Has.Some.Contain("Transcript size"));
    }
}
