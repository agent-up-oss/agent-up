using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;
using AgentUp.Desktop.Features.Agents.Services;
using AgentUp.Desktop.Features.Agents.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentChatViewModelTests
{
    [Test]
    public async Task LoadAsync_notifiesComputedPropertiesAfterClearingSessionContext()
    {
        var notifications = new List<string>();
        var view = CreateView(new ChatApiFake());
        view.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
                notifications.Add(args.PropertyName);
        };

        await view.LoadAsync("ws-1");

        Assert.That(notifications, Does.Contain(nameof(view.HasPermission)));
        Assert.That(notifications, Does.Contain(nameof(view.HasContext)));
        Assert.That(notifications, Does.Contain(nameof(view.HasMessages)));
        Assert.That(view.HasPermission, Is.False);
        Assert.That(view.HasContext, Is.False);
        Assert.That(view.HasMessages, Is.False);
    }

    [Test]
    public async Task SendAsync_reportsCanceledHttpCalls()
    {
        var view = CreateView(new ChatApiFake { SendFailure = new TaskCanceledException("The request was canceled.") });
        await view.LoadAsync("ws-1");
        view.Message = "hello";

        await view.SendCommand.Execute().FirstAsync();

        Assert.That(view.Error, Is.EqualTo("The request was canceled."));
    }

    [Test]
    public async Task LoadAsync_waitsForThePreviousStreamLoopBeforeResetting()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new ChatApiFake { EventsHang = hang };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");
        var reload = view.LoadAsync("ws-2");
        await reload;

        Assert.That(fake.LastWorkspace, Is.EqualTo("ws-2"));
        Assert.That(view.HasPermission, Is.False);
    }

    [Test]
    public async Task LoadAsync_ignoresErrorsFromSupersededLoads()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new ChatApiFake
        {
            FirstGetHang = hang,
            GetFailures = { ["ws-1"] = new HttpRequestException("stale workspace") }
        };
        var view = CreateView(fake);
        var first = view.LoadAsync("ws-1");
        await view.LoadAsync("ws-2");
        hang.SetResult();
        await first;

        Assert.That(view.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_keepsTheAgentCatalogAndHidesAnEmptyTranscript()
    {
        var fake = new ChatApiFake
        {
            Session = new AgentSessionDto("ws-1", null, "idle", null, null, [
                new AgentDescriptorDto("Codex", true, "Codex"),
                new AgentDescriptorDto("Cursor", false, "Cursor")
            ], [])
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.False);
            Assert.That(view.HasMessages, Is.False);
            Assert.That(view.StatusLine, Is.EqualTo("Idle"));
            Assert.That(view.Agents.Select(agent => agent.DisplayName), Is.EqualTo(["Codex", "Cursor"]));
        });
    }

    [Test]
    public async Task StopAsync_keepsTheAgentCatalogAndClearsTheTranscript()
    {
        var fake = new ChatApiFake
        {
            Session = new AgentSessionDto("ws-1", "Codex", "ready", "s1", null, [
                new AgentDescriptorDto("Codex", true, "Codex")
            ], [])
        };
        var view = CreateView(fake);
        await view.LoadAsync("ws-1");

        await view.StopCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.False);
            Assert.That(view.HasMessages, Is.False);
            Assert.That(view.StatusLine, Is.EqualTo("Idle"));
            Assert.That(view.Agents, Has.Count.EqualTo(1));
            Assert.That(view.Agents[0].DisplayName, Is.EqualTo("Codex"));
        });
    }

    [Test]
    public async Task EmptyFollowUpSnapshot_doesNotClearTheAgentCatalog()
    {
        var fake = new ChatApiFake
        {
            Session = new AgentSessionDto("ws-1", null, "idle", null, null, [
                new AgentDescriptorDto("Codex", true, "Codex")
            ], []),
            NextSession = new AgentSessionDto("ws-1", "Codex", "ready", "s1", null, [], [])
        };
        var view = CreateView(fake);
        await view.LoadAsync("ws-1");

        await view.SelectAgentCommand.Execute("Codex").FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.True);
            Assert.That(view.StatusLine, Is.EqualTo("Codex · Idle"));
            Assert.That(view.SelectedAgentDisplayName, Is.EqualTo("Codex"));
            Assert.That(view.Agents, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ChatItem_usesDisplayRoleWhenPresent()
    {
        Assert.That(new AgentChatItemViewModel("Agent", "Working", displayRole: "Codex").Label, Is.EqualTo("Codex"));
        Assert.That(new AgentChatItemViewModel("You", "Hello").Label, Is.EqualTo("You"));
        Assert.That(new AgentChatItemViewModel("You", "Hello").IsUser, Is.True);
        Assert.That(new AgentChatItemViewModel("You", "Hello").IsWork, Is.False);
    }

    [Test]
    public void Run_staysOpenUntilTheNextQuestionSealsIt()
    {
        var run = new AgentRunViewModel();
        run.Items.Add(new AgentChatItemViewModel("Thought", "Looking"));
        run.Items.Add(new AgentChatItemViewModel("Tool", "Search", "completed", "t1"));
        run.Items.Add(new AgentChatItemViewModel("Agent", "Done", displayRole: "Codex"));

        Assert.Multiple(() =>
        {
            Assert.That(run.IsLive, Is.True);
            Assert.That(run.ShowHeader, Is.False);
            Assert.That(run.IsExpanded, Is.True);
            Assert.That(run.HasReply, Is.True);
            Assert.That(run.Reply!.Text, Is.EqualTo("Done"));
            Assert.That(run.WorkItems, Has.Count.EqualTo(2));
            Assert.That(run.Summary, Is.EqualTo("Worked · 1 tool · Thought"));
            Assert.That(run.Chevron, Is.EqualTo("▾"));
        });

        run.Seal();
        Assert.That(run.ShowHeader, Is.True);
        Assert.That(run.IsExpanded, Is.False);
        Assert.That(run.HasReply, Is.True);
        Assert.That(run.Reply!.Text, Is.EqualTo("Done"));
        Assert.That(run.Chevron, Is.EqualTo("▸"));
    }

    [Test]
    public async Task ChatItem_collapsesThoughtsUntilToggled()
    {
        var thought = new AgentChatItemViewModel("Thought", "**Clarifying test meaning**");

        Assert.Multiple(() =>
        {
            Assert.That(thought.IsThought, Is.True);
            Assert.That(thought.IsWork, Is.False);
            Assert.That(thought.VisibleText, Is.EqualTo("Clarifying test meaning"));
            Assert.That(thought.IsThoughtExpanded, Is.False);
            Assert.That(thought.Label, Is.EqualTo("Thought"));
        });

        thought.SetLive(true);
        Assert.That(thought.IsThoughtExpanded, Is.True);
        Assert.That(thought.Label, Is.EqualTo("Thinking"));

        await thought.ToggleCommand.Execute().FirstAsync();
        Assert.That(thought.IsThoughtExpanded, Is.False);
        Assert.That(thought.Label, Is.EqualTo("Thinking"));

        thought.SetLive(false);
        Assert.That(thought.Label, Is.EqualTo("Thought"));
        await thought.ToggleCommand.Execute().FirstAsync();
        Assert.That(thought.IsThoughtExpanded, Is.True);
        Assert.That(thought.Label, Is.EqualTo("Thought"));
    }

    private static AgentChatViewModel CreateView(IAgentApiProvider provider)
        => new(new AgentsController(new AgentChatService(provider)));
}

internal sealed class ChatApiFake : IAgentApiProvider
{
    public Exception? SendFailure { get; set; }
    public TaskCompletionSource? EventsHang { get; set; }
    public TaskCompletionSource? FirstGetHang { get; set; }
    public Dictionary<string, Exception> GetFailures { get; } = new(StringComparer.Ordinal);
    public string? LastWorkspace { get; private set; }
    private int _gets;

    public AgentSessionDto? Session { get; set; }
    public AgentSessionDto? NextSession { get; set; }

    public async Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _gets) == 1 && FirstGetHang is not null)
            await FirstGetHang.Task;
        LastWorkspace = workspaceId;
        if (GetFailures.TryGetValue(workspaceId, out var failure))
            throw failure;
        return Session ?? new AgentSessionDto(workspaceId, null, "idle", null, null, [], []);
    }

    public Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken)
        => Task.FromResult(NextSession ?? Session);

    public Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken)
        => SendFailure is null ? Task.CompletedTask : Task.FromException(SendFailure);

    public Task AuthenticateAsync(string workspaceId, string methodId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(string workspaceId, CancellationToken cancellationToken) => Task.CompletedTask;

    public async IAsyncEnumerable<AgentEventDto> EventsAsync(
        string workspaceId,
        long after,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (EventsHang is not null)
            await EventsHang.Task.WaitAsync(cancellationToken);
        yield break;
    }
}
