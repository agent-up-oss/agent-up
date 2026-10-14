using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class MemoryAssignedProposalQueueTests
{
    [Test]
    public async Task ListAwaitingAsync_ordersByCreatedThenId()
    {
        var queue = new MemoryAssignedProposalQueue();
        queue.Replace("ws", [
            new AssignedProposal("b", "Later", "", DateTimeOffset.UnixEpoch.AddDays(2), "https://example/b"),
            new AssignedProposal("a", "Earlier", "", DateTimeOffset.UnixEpoch.AddDays(1), "https://example/a")
        ]);

        var items = await queue.ListAwaitingAsync("ws", CancellationToken.None);

        Assert.That(items.Select(item => item.Id), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public async Task ListAwaitingAsync_returnsEmptyWhenTheWorkspaceHasNoProposals()
    {
        var queue = new MemoryAssignedProposalQueue();

        var items = await queue.ListAwaitingAsync("missing", CancellationToken.None);

        Assert.That(items, Is.Empty);
    }
}
