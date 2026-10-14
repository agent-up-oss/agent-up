using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class MemoryRoleProgressTests
{
    [Test]
    public void Satisfied_isFalseUntilTheTerminationIsRecorded()
    {
        var progress = new MemoryRoleProgress();

        Assert.That(progress.Satisfied("ws", RoleTerminationKind.ProposalOpened), Is.False);
        progress.Record("ws", RoleTerminationKind.ProposalOpened);
        Assert.That(progress.Satisfied("ws", RoleTerminationKind.ProposalOpened), Is.True);
        Assert.That(progress.Satisfied("ws", RoleTerminationKind.ReviewSubmitted), Is.False);
    }

    [Test]
    public void Record_doesNotLeakAcrossWorkspaces()
    {
        var progress = new MemoryRoleProgress();
        progress.Record("alpha", RoleTerminationKind.ReviewSubmitted);

        Assert.That(progress.Satisfied("beta", RoleTerminationKind.ReviewSubmitted), Is.False);
    }
}
