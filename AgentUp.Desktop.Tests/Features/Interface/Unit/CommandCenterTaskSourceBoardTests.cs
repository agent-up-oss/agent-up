using System.Reactive.Linq;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

[TestFixture]
public sealed class CommandCenterTaskSourceBoardTests
{
    [Test]
    public async Task RepositorySlots_nameTheHostAndTheOpenKey()
    {
        var board = new CommandCenterTaskSourceBoard();
        await board.EnsureSourceAsync(Source(new FakeTaskSourceApi
        {
            Repositories =
            [
                new TaskSourceRepository("github", "example", "repository"),
                new TaskSourceRepository("forgejo", "acme", "widgets"),
            ],
        }));

        var slots = board.RepositorySlots();

        Assert.Multiple(() =>
        {
            Assert.That(slots[0].Id, Is.EqualTo("github/example/repository"));
            Assert.That(slots[0].Host, Is.EqualTo("GitHub"));
            Assert.That(slots[1].Host, Is.EqualTo("Forgejo"));
            Assert.That(slots[1].Name, Is.EqualTo("acme/widgets"));
        });
    }

    [Test]
    public void OpenRepository_readsTheKeyAndCloseRepository_returnsToTheList()
    {
        var board = new CommandCenterTaskSourceBoard();
        var queue = Queue();

        Assert.Multiple(() =>
        {
            Assert.That(board.OpenRepository("github/example/repository", queue), Is.True);
            Assert.That(board.OpenRepositoryLabel, Is.EqualTo("example/repository"));
            Assert.That(board.TasksTab, Is.EqualTo("issues"));
            Assert.That(board.OpenRepository("github/example", queue), Is.False);
        });

        board.CloseRepository(queue);

        Assert.That(board.OpenRepositoryLabel, Is.Null);
    }

    [Test]
    public void ShowForgeTab_ignoresAnEmptyIdAndShowTasksTab_opensThatQueue()
    {
        var board = new CommandCenterTaskSourceBoard();
        board.ShowForgeTab("forgejo");
        board.ShowForgeTab("");
        board.ShowTasksTab("dev");

        Assert.Multiple(() =>
        {
            Assert.That(board.ForgeTab, Is.EqualTo("forgejo"));
            Assert.That(board.TasksTab, Is.EqualTo("dev"));
        });
    }

    [Test]
    public async Task TasksOf_namesWorkingQueuedAndOfferedRowsForTheOpenRepository()
    {
        var api = new FakeTaskQueueApi
        {
            Queue =
            [
                Queued("1", 1) with { HeldByWorkspaceId = "ws-bob" },
                Queued("2", 2),
            ],
            RepositoryIssues = [new OfferedIssue("57", 57, "From the repository", "u")],
        };
        var queue = Queue(api);
        queue.SelectRepository("github", "example", "repository");
        await queue.ReadAsync();
        var board = new CommandCenterTaskSourceBoard();
        board.OpenRepository("github/example/repository", queue);

        var rows = board.TasksOf(queue, [TeamStandingComposition.Standing(workspaceId: "ws-bob", displayName: "Bob")]);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(row => row.Action), Is.EqualTo(new[] { "stop", "dequeue", "queue" }));
            Assert.That(rows[0].Agent, Is.EqualTo("Bob"));
            Assert.That(board.TasksOf(null, []), Is.Empty);
        });
    }

    [Test]
    public async Task SaveTokenAsync_sendsTheHostUrlAndTokenAndForgetDropsThatHost()
    {
        var api = new FakeTaskSourceApi { State = Connected(false) };
        var source = Source(api);
        var board = new CommandCenterTaskSourceBoard();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["forge-endpoint"] = "https://github.com",
            ["save-token"] = "secret-token",
        };

        await board.SaveTokenAsync(source, fields, null);
        await board.ForgetTokenAsync(source);

        Assert.Multiple(() =>
        {
            Assert.That(api.Saved, Is.EqualTo(("github", "https://github.com", "secret-token")));
            Assert.That(api.Forgotten, Is.EqualTo("github"));
        });
    }

    [Test]
    public async Task SaveTokenAsync_doesNothingWhenTheTokenIsEmpty()
    {
        var api = new FakeTaskSourceApi();
        var board = new CommandCenterTaskSourceBoard();

        await board.SaveTokenAsync(Source(api), new Dictionary<string, string>(), "  ");
        await board.SaveTokenAsync(null, null, "token");
        await board.ForgetTokenAsync(null);

        Assert.That(api.Saved, Is.Null);
    }

    [Test]
    public async Task LoadOpenRepositoryAsync_wordsTheReviewsAndClearsThemWhenTheHostCannotBeReached()
    {
        var api = new FakeTaskSourceApi
        {
            Pulls =
            [
                new TaskSourcePull("42", 42, "Product cards", "u",
                    [new TaskSourceReview("devReviewer", "reviewing", "hector", null)]),
            ],
        };
        var board = new CommandCenterTaskSourceBoard();
        board.OpenRepository("github/example/repository", null);

        await board.LoadOpenRepositoryAsync(null, null);
        Assert.That(board.VisiblePmPulls, Is.Empty);
        await board.LoadOpenRepositoryAsync(null, Source(api));
        var reviewing = board.DevPulls.Single().Agent;
        api.PullsFailure = new HttpRequestException("offline");
        await board.LoadOpenRepositoryAsync(null, Source(api));
        api.PullsFailure = new TaskCanceledException();
        board.OpenRepository("github/example/repository", null);
        await board.LoadOpenRepositoryAsync(null, Source(api));

        Assert.Multiple(() =>
        {
            Assert.That(reviewing, Is.EqualTo("hector"));
            Assert.That(board.DevPulls, Is.Empty);
            Assert.That(board.VisibleDevPulls, Is.Empty);
        });
    }

    [Test]
    public async Task EnsureSourceAsync_loadsRepositoriesOnce()
    {
        var api = new FakeTaskSourceApi
        {
            State = Connected(),
            Repositories = [new TaskSourceRepository("github", "example", "repository")],
        };
        var source = Source(api);
        var board = new CommandCenterTaskSourceBoard();

        await board.EnsureSourceAsync(source);
        await board.EnsureSourceAsync(source);
        await board.EnsureSourceAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(api.Reads, Is.EqualTo(1));
            Assert.That(board.Repositories.Single().Name, Is.EqualTo("repository"));
        });
    }

    [Test]
    public async Task PickOffered_queuesTheChosenIssue()
    {
        var api = new FakeTaskQueueApi { RepositoryIssues = [new OfferedIssue("57", 57, "From the repository", "u")] };
        var queue = Queue(api);
        queue.SelectRepository("github", "example", "repository");
        await queue.ReadAsync();
        var board = new CommandCenterTaskSourceBoard();

        board.PickOffered(queue, "0");
        board.PickOffered(queue, "nope");
        await queue.Offered.Single().PickCommand.Execute();

        Assert.That(api.Enqueued, Has.Count.GreaterThanOrEqualTo(1));
    }

    private static TaskQueueViewModel Queue(FakeTaskQueueApi? api = null)
        => new(new TaskQueueController(new TaskQueueService(api ?? new FakeTaskQueueApi())));

    private static TaskSourceViewModel Source(FakeTaskSourceApi api)
        => new(new TaskSourceController(new TaskSourceService(api)));

    private static QueuedTask Queued(string id, int number) =>
        new(id, "example", "repository", number, $"Task {number}", "u", null, null);

    private static TaskSource Connected(bool github = true) => new(
    [
        new TaskSourceModule("github", "GitHub", github, github ? "Reading." : "Paste a token.", github ? "https://github.com" : null),
        new TaskSourceModule("forgejo", "Forgejo", false, "Enable Forgejo."),
    ]);

    private sealed class FakeTaskSourceApi : ITaskSourceApiProvider
    {
        public TaskSource? State { get; set; }

        public IReadOnlyList<TaskSourceRepository> Repositories { get; init; } = [];

        public IReadOnlyList<TaskSourcePull> Pulls { get; init; } = [];

        public Exception? PullsFailure { get; set; }

        public (string Module, string Endpoint, string Token)? Saved { get; private set; }

        public string? Forgotten { get; private set; }

        public int Reads { get; private set; }

        public Task<TaskSource?> ReadAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(State);
        }

        public Task<TaskSource?> SaveAsync(string moduleId, string endpointUrl, string token, CancellationToken cancellationToken)
        {
            Saved = (moduleId, endpointUrl, token);
            State = Connected();
            return Task.FromResult<TaskSource?>(State);
        }

        public Task<TaskSource?> ForgetAsync(string moduleId, CancellationToken cancellationToken)
        {
            Forgotten = moduleId;
            State = Connected(false);
            return Task.FromResult<TaskSource?>(State);
        }

        public Task<IReadOnlyList<TaskSourceRepository>> ListRepositoriesAsync(CancellationToken cancellationToken)
            => Task.FromResult(Repositories);

        public Task<IReadOnlyList<TaskSourcePull>> ListPullsAsync(
            string moduleId,
            string owner,
            string repository,
            CancellationToken cancellationToken)
            => PullsFailure is not null
                ? Task.FromException<IReadOnlyList<TaskSourcePull>>(PullsFailure)
                : Task.FromResult(Pulls);
    }

    private sealed class FakeTaskQueueApi : ITaskQueueApiProvider
    {
        public IReadOnlyList<QueuedTask> Queue { get; init; } = [];

        public IReadOnlyList<OfferedIssue> RepositoryIssues { get; init; } = [];

        public List<(string? WorkspaceId, string IssueId, string? ModuleId)> Enqueued { get; } = [];

        public Task<IReadOnlyList<QueuedTask>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Queue);

        public Task<IReadOnlyList<OfferedIssue>> ListIssuesAsync(string workspaceId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OfferedIssue>>([]);

        public Task<IReadOnlyList<OfferedIssue>> ListRepositoryIssuesAsync(
            string moduleId,
            string owner,
            string repository,
            CancellationToken cancellationToken) =>
            Task.FromResult(RepositoryIssues);

        public Task EnqueueAsync(
            string? workspaceId,
            string issueId,
            string? targetWorkspaceId,
            CancellationToken cancellationToken,
            string? moduleId = null,
            string? owner = null,
            string? repository = null)
        {
            Enqueued.Add((workspaceId, issueId, moduleId));
            return Task.CompletedTask;
        }

        public Task RemoveAsync(QueuedTask task, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MoveAsync(QueuedTask task, int index, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task TargetAsync(QueuedTask task, string? workspaceId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAsync(QueuedTask task, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
