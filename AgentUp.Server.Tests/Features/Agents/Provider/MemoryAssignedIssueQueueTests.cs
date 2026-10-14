using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class MemoryAssignedIssueQueueTests
{
    [Test]
    public async Task ListAssignedAsync_ordersByTheDeclaredKey()
    {
        var queue = new MemoryAssignedIssueQueue();
        var older = Issue(2, created: 1, updated: 9);
        var newer = Issue(1, created: 5, updated: 2);
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

    private static AssignedIssue Issue(int number, int created, int updated) => new(
        $"{number}",
        number,
        $"Issue {number}",
        "",
        DateTimeOffset.UnixEpoch.AddDays(created),
        DateTimeOffset.UnixEpoch.AddDays(updated),
        $"https://example/{number}");
}
