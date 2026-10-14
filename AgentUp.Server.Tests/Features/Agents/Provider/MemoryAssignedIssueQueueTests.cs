using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class MemoryAssignedIssueQueueTests
{
    [Test]
    public async Task ListAssignedAsync_ordersByTheDeclaredKey()
    {
        var queue = new MemoryAssignedIssueQueue();
        var older = ServerDomain.Issue().Numbered(2).Created(1).Updated(9).Build();
        var newer = ServerDomain.Issue().Numbered(1).Created(5).Updated(2).Build();
        queue.Replace("ws", [older, newer]);

        var byNumber = await queue.ListAssignedAsync("ws", AssignedIssueOrder.NumberAsc, CancellationToken.None);
        var byCreated = await queue.ListAssignedAsync("ws", AssignedIssueOrder.CreatedAsc, CancellationToken.None);
        var byUpdated = await queue.ListAssignedAsync("ws", AssignedIssueOrder.UpdatedAsc, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(byNumber.Select(item => item.Number), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(byCreated.Select(item => item.Number), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(byUpdated.Select(item => item.Number), Is.EqualTo(new[] { 1, 2 }));
        });
    }

    [Test]
    public async Task ListAssignedAsync_returnsEmptyWhenTheWorkspaceHasNoIssues()
    {
        var queue = new MemoryAssignedIssueQueue();

        var items = await queue.ListAssignedAsync("missing", AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.That(items, Is.Empty);
    }
}
