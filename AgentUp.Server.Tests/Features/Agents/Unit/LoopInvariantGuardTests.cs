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

    [Test]
    public void Evaluate_failsWhenTheTerminalIsUntyped()
    {
        var violations = new LoopInvariantGuard().Evaluate(
            new LoopRunResultBuilder().Terminal((LoopTerminal)42).Build());

        Assert.That(violations, Has.Some.Contain("untyped terminal"));
    }

    [Test]
    public void Evaluate_failsWhenANonPolicyStopHasNoReason()
    {
        var violations = new LoopInvariantGuard().Evaluate(
            new LoopRunResultBuilder().Terminal(LoopTerminal.Failed).WithoutStopReason().Build());

        Assert.That(violations, Has.Some.Contain("typed stop reason"));
    }

    [Test]
    public void Evaluate_failsWhenContextAccountingWentNegative()
    {
        var violations = new LoopInvariantGuard().Evaluate(new LoopRunResultBuilder().Context(-1).Build());

        Assert.That(violations, Has.Some.Contain("negative"));
    }

    [Test]
    public void Evaluate_failsWhenCompletedDidNotRecordACompletedTask()
    {
        var violations = new LoopInvariantGuard().Evaluate(
            new LoopRunResultBuilder().WithoutCompletedTask().Build());

        Assert.That(violations, Has.Some.Contain("completed task"));
    }

    [Test]
    public void Evaluate_failsWhenAPolicyBlockIsNotTyped()
    {
        var violations = new LoopInvariantGuard().Evaluate(
            new LoopRunResultBuilder().Terminal(LoopTerminal.BlockedPolicy).WithoutBlockReason().Build());

        Assert.That(violations, Has.Some.Contain("blocked terminal"));
    }

    [Test]
    public void Evaluate_failsWhenAPermissionRuleIdIsBlank()
    {
        var violations = new LoopInvariantGuard().Evaluate(new LoopRunResultBuilder().WithoutRuleIds().Build());

        Assert.That(violations, Has.Some.Contain("recorded rule id"));
    }
}
