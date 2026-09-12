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
        Assert.That(view.HasPermission, Is.False);
        Assert.That(view.HasContext, Is.False);
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

    private static AgentChatViewModel CreateView(IAgentApiProvider provider)
        => new(new AgentsController(new AgentChatService(provider)));
}

internal sealed class ChatApiFake : IAgentApiProvider
{
    public Exception? SendFailure { get; set; }
    public TaskCompletionSource? EventsHang { get; set; }
    public string? LastWorkspace { get; private set; }

    public Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        LastWorkspace = workspaceId;
        return Task.FromResult<AgentSessionDto?>(new AgentSessionDto(workspaceId, null, "idle", null, null, [], []));
    }

    public Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken)
        => Task.FromResult<AgentSessionDto?>(null);

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
