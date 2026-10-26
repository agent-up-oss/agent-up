using System.Reactive.Linq;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Interface.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

[TestFixture]
public sealed class TaskQueueViewModelTests
{
    [Test]
    public async Task ReadAsync_showsTheQueueInTheOrderTheServerHoldsIt()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 9), Queued("2", 2)] };
        var queue = ViewModel(api);

        await queue.ReadAsync();

        Assert.That(queue.Queue.Select(row => row.TaskLabel), Is.EqualTo(new[] { "#9 · Task 9", "#2 · Task 2" }));
    }

    [Test]
    public async Task ReadAsync_namesTheWorkspaceHoldingATaskRatherThanItsId()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 1) with { HeldByWorkspaceId = "ws-bob" } ] };
        var queue = ViewModel(api);
        queue.Attach([new TeamMember("ws-bob", "Bob", "feat/44", "Running")]);

        await queue.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(queue.Queue.Single().HolderLabel, Is.EqualTo("Bob"));
            Assert.That(queue.Queue.Single().IsHeld, Is.True);
        });
    }

    [Test]
    public async Task ReadAsync_saysATaskAnybodyMayTakeIsForAnyoneAndNotHeld()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 1)] };
        var queue = ViewModel(api);

        await queue.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(queue.Queue.Single().TargetLabel, Is.EqualTo("anyone"));
            Assert.That(queue.Queue.Single().HolderLabel, Is.EqualTo("free"));
        });
    }

    [Test]
    public async Task ReadAsync_doesNotOfferAnIssueThatIsAlreadyQueued()
    {
        var api = new FakeTaskQueueApi
        {
            Queue = [Queued("7", 7)],
            Issues = [new OfferedIssue("7", 7, "Task 7", "u"), new OfferedIssue("8", 8, "Task 8", "u")]
        };
        var queue = ViewModel(api);
        queue.Attach([new TeamMember("ws-bob", "Bob", "feat/44", "Running")]);

        await queue.ReadAsync();

        Assert.That(queue.Offered.Select(row => row.IssueLabel), Is.EqualTo(new[] { "#8 · Task 8" }));
    }

    [Test]
    public async Task ReadAsync_offersNothingUntilAWorkspaceIsOpen()
    {
        var api = new FakeTaskQueueApi { Issues = [new OfferedIssue("8", 8, "Task 8", "u")] };
        var queue = ViewModel(api);

        await queue.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(queue.Offered, Is.Empty);
            Assert.That(queue.EmptyOfferedHint, Does.Contain("Open a workspace"));
        });
    }

    [Test]
    public async Task PickCommand_queuesTheIssueAgainstTheOpenWorkspace()
    {
        var api = new FakeTaskQueueApi { Issues = [new OfferedIssue("8", 8, "Task 8", "u")] };
        var queue = ViewModel(api);
        queue.Attach([new TeamMember("ws-bob", "Bob", "feat/44", "Running")]);
        await queue.ReadAsync();

        await queue.Offered.Single().PickCommand.Execute();

        Assert.That(api.Enqueued, Is.EqualTo(new[] { ("ws-bob", "8") }));
    }

    [Test]
    public async Task MoveUpCommand_asksForThePositionAboveTheOneItHolds()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 1), Queued("2", 2)] };
        var queue = ViewModel(api);
        await queue.ReadAsync();

        await queue.Queue[1].MoveUpCommand.Execute();

        Assert.That(api.Moved, Is.EqualTo(new[] { ("2", 0) }));
    }

    [Test]
    public async Task ReadAsync_reportsAServerThatCannotBeReached()
    {
        var api = new FakeTaskQueueApi { Failure = new HttpRequestException("offline") };
        var queue = ViewModel(api);

        await queue.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(queue.HasError, Is.True);
            Assert.That(queue.ErrorMessage, Is.EqualTo("offline"));
        });
    }

    [Test]
    public async Task ReadAsync_aSupersededReadDoesNotOverwriteANewerOne()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 1)] };
        var queue = ViewModel(api);
        await queue.ReadAsync();
        api.Queue = [Queued("2", 2)];

        var stale = queue.ReadAsync();
        api.Queue = [Queued("3", 3)];
        await queue.ReadAsync();
        await stale;

        Assert.That(queue.Queue.Single().TaskLabel, Is.EqualTo("#3 · Task 3"));
    }

    [Test]
    public async Task RemoveCommand_takesTheTaskOutOfTheQueue()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 1)] };
        var queue = ViewModel(api);
        await queue.ReadAsync();

        await queue.Queue.Single().RemoveCommand.Execute();

        Assert.That(api.Removed, Is.EqualTo(new[] { "1" }));
    }

    [Test]
    public async Task ReleaseCommand_handsAHeldTaskBack()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 1) with { HeldByWorkspaceId = "ws-bob" }] };
        var queue = ViewModel(api);
        await queue.ReadAsync();

        await queue.Queue.Single().ReleaseCommand.Execute();

        Assert.That(api.Released, Is.EqualTo(new[] { "1" }));
    }

    [Test]
    public async Task MoveDownCommand_asksForThePositionBelowTheOneItHolds()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 1), Queued("2", 2)] };
        var queue = ViewModel(api);
        await queue.ReadAsync();

        await queue.Queue[0].MoveDownCommand.Execute();

        Assert.That(api.Moved, Is.EqualTo(new[] { ("1", 1) }));
    }

    [Test]
    public async Task AChangeTheServerRefusesIsReportedAndTheListIsReadBack()
    {
        var api = new FakeTaskQueueApi { Queue = [Queued("1", 1)], ChangeFailure = new HttpRequestException("refused") };
        var queue = ViewModel(api);
        await queue.ReadAsync();

        await queue.Queue.Single().RemoveCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(queue.HasError, Is.True);
            Assert.That(queue.ErrorMessage, Is.EqualTo("refused"));
            Assert.That(queue.Queue.Single().TaskLabel, Is.EqualTo("#1 · Task 1"));
        });
    }

    [Test]
    public async Task PickCommand_doesNothingWhileNoWorkspaceIsOpen()
    {
        var api = new FakeTaskQueueApi { Issues = [new OfferedIssue("8", 8, "Task 8", "u")] };
        var queue = ViewModel(api);
        queue.Attach([new TeamMember("ws-bob", "Bob", "feat/44", "Running")]);
        await queue.ReadAsync();
        var row = queue.Offered.Single();
        queue.Attach([]);

        await row.PickCommand.Execute();

        Assert.That(api.Enqueued, Is.Empty);
    }

    [Test]
    public async Task TheEmptyQueueHintSaysToPickSomethingOnceTheQueueHasBeenRead()
    {
        var queue = ViewModel(new FakeTaskQueueApi());

        var before = queue.EmptyQueueHint;
        await queue.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(before, Does.Contain("Reading"));
            Assert.That(queue.EmptyQueueHint, Does.Contain("Pick an issue"));
        });
    }

    private static TaskQueueViewModel ViewModel(ITaskQueueApiProvider api) => new(new TaskQueueController(new TaskQueueService(api)));

    private static QueuedTask Queued(string id, int number) =>
        new(id, "acme", "widgets", number, $"Task {number}", "u", null, null);

    private sealed class FakeTaskQueueApi : ITaskQueueApiProvider
    {
        public IReadOnlyList<QueuedTask> Queue { get; set; } = [];

        public IReadOnlyList<OfferedIssue> Issues { get; init; } = [];

        public Exception? Failure { get; init; }

        public List<(string WorkspaceId, string IssueId)> Enqueued { get; } = [];

        public List<(string Id, int Index)> Moved { get; } = [];

        public List<string> Removed { get; } = [];

        public List<string> Released { get; } = [];

        public Exception? ChangeFailure { get; init; }

        public Task<IReadOnlyList<QueuedTask>> ListAsync(CancellationToken cancellationToken) =>
            Failure is not null ? Task.FromException<IReadOnlyList<QueuedTask>>(Failure) : Task.FromResult(Queue);

        public Task<IReadOnlyList<OfferedIssue>> ListIssuesAsync(string workspaceId, CancellationToken cancellationToken) =>
            Task.FromResult(Issues);

        public Task EnqueueAsync(string workspaceId, string issueId, string? targetWorkspaceId, CancellationToken cancellationToken)
        {
            Enqueued.Add((workspaceId, issueId));
            return Task.CompletedTask;
        }

        public Task RemoveAsync(QueuedTask task, CancellationToken cancellationToken)
        {
            if (ChangeFailure is not null) return Task.FromException(ChangeFailure);
            Removed.Add(task.Id);
            return Task.CompletedTask;
        }

        public Task MoveAsync(QueuedTask task, int index, CancellationToken cancellationToken)
        {
            Moved.Add((task.Id, index));
            return Task.CompletedTask;
        }

        public Task TargetAsync(QueuedTask task, string? workspaceId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAsync(QueuedTask task, CancellationToken cancellationToken)
        {
            Released.Add(task.Id);
            return Task.CompletedTask;
        }
    }
}
