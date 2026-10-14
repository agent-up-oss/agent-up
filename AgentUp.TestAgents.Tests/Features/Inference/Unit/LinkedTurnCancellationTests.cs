using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Tests.Features.Inference.Unit;

[TestFixture]
public sealed class LinkedTurnCancellationTests
{
    [Test]
    public void Cancel_signalsTheCurrentTurn()
    {
        using var turns = new LinkedTurnCancellation(CancellationToken.None);
        turns.Cancel();

        Assert.That(turns.Token.IsCancellationRequested, Is.True);
    }

    [Test]
    public void BeginNext_replacesTheCancelledTurn()
    {
        using var turns = new LinkedTurnCancellation(CancellationToken.None);
        turns.Cancel();
        turns.BeginNext();

        Assert.That(turns.Token.IsCancellationRequested, Is.False);
    }
}
