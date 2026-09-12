using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;
using AgentUp.Desktop.Features.Agents.Services;
using AgentUp.Desktop.Features.Agents.ViewModels;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;

namespace AgentUp.Desktop.Tests.Features.Agents.Headless;

[TestFixture]
public sealed class AgentChatViewModelStreamTests
{
    [AvaloniaTest]
    public async Task Stream_appliesSessionMessagesToolsPlansAndPermissions()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new StreamApiFake
        {
            EventsHang = hang,
            Events =
            [
                Event(1, "state", """{"workspaceId":"ws-1","agent":"Codex","state":"running","sessionId":"s1","error":null,"agents":[{"agent":"Codex","available":true,"displayName":"Codex"}],"authMethods":[]}"""),
                Event(2, "user_message", """{"text":"Hello"}"""),
                Event(3, "session_update", """{"sessionUpdate":"session_info_update","title":"Fix tests"}"""),
                Event(4, "session_update", """{"sessionUpdate":"current_mode_update","currentModeId":"ask"}"""),
                Event(5, "session_update", """{"sessionUpdate":"usage_update","used":12,"size":20}"""),
                Event(6, "session_update", """{"sessionUpdate":"agent_message_chunk","content":{"text":"Working"}}"""),
                Event(7, "session_update", """{"sessionUpdate":"agent_message_chunk","content":{"text":" on it"}}"""),
                Event(8, "session_update", """{"sessionUpdate":"tool_call","toolCallId":"t1","title":"Read","status":"pending"}"""),
                Event(9, "session_update", """{"sessionUpdate":"tool_call_update","toolCallId":"t1","status":"completed","content":{"text":"ok"}}"""),
                Event(10, "session_update", """{"sessionUpdate":"plan_update","entries":[{"content":"One","status":"completed"}]}"""),
                Event(11, "session_update", """{"sessionUpdate":"compaction_start"}"""),
                Event(12, "permission_request", """{"requestId":"req-1","request":{"title":"Run tests?","options":[{"optionId":"allow","name":"Allow once","kind":"allow_once"}]}}"""),
            ]
        };
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));

        await view.LoadAsync("ws-1");
        await WaitUntilAsync(() => view.HasPermission && view.Messages.Count >= 4);

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.True);
            Assert.That(view.HasSession, Is.True);
            Assert.That(view.HasContext, Is.True);
            Assert.That(view.SessionTitle, Is.EqualTo("Fix tests"));
            Assert.That(view.Mode, Is.EqualTo("ask"));
            Assert.That(view.Messages.Any(item => item.Role == "You" && item.Text == "Hello"), Is.True);
            Assert.That(view.Messages.Any(item => item.Role == "Agent" && item.Text.Contains("Working on it")), Is.True);
            Assert.That(view.Messages.Any(item => item.Role == "Tool" && item.ToolCallId == "t1"), Is.True);
            Assert.That(view.Messages.Any(item => item.Role == "Plan"), Is.True);
            Assert.That(view.PermissionTitle, Is.EqualTo("Run tests?"));
            Assert.That(view.HasPermission, Is.True);
        });

        hang.TrySetResult();
        await view.LoadAsync(null);
        Assert.That(view.HasAgent, Is.False);
    }

    [AvaloniaTest]
    public async Task LoadAsync_exposesAuthenticationMethods()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new StreamApiFake
        {
            EventsHang = hang,
            Session = new AgentSessionDto("ws-1", "Codex", "authentication_required", null, null, [
                new AgentDescriptorDto("Codex", true, "Codex")
            ], [new AgentAuthMethodDto("chatgpt", "ChatGPT", "Use a subscription.")])
        };
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));

        await view.LoadAsync("ws-1");
        await view.AuthenticationOptions[0].Command.Execute().FirstAsync();

        Assert.That(fake.Authenticated, Is.EqualTo(("ws-1", "chatgpt")));
        hang.TrySetResult();
        await view.LoadAsync(null);
    }

    [AvaloniaTest]
    public async Task Stream_appliesThoughtsPlansAndPermissionDecisions()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new StreamApiFake
        {
            EventsHang = hang,
            Events =
            [
                Event(1, "state", """{"workspaceId":"ws-1","agent":"Codex","state":"running","sessionId":"s1","error":null,"agents":[{"agent":"Codex","available":true,"displayName":"Codex"}],"authMethods":[]}"""),
                Event(2, "session_update", """{"sessionUpdate":"agent_thought_chunk","content":{"text":"Hmm"}}"""),
                Event(3, "session_update", """{"sessionUpdate":"agent_thought_chunk","content":{"text":"..."}}"""),
                Event(4, "session_update", """{"sessionUpdate":"plan_update","entries":[{"content":"One","status":"pending"}]}"""),
                Event(5, "session_update", """{"sessionUpdate":"plan_update","entries":[{"content":"Two","status":"in_progress"}]}"""),
                Event(6, "session_update", """{"sessionUpdate":"compaction_start"}"""),
                Event(7, "session_update", """{"sessionUpdate":"compaction_completed_summary"}"""),
                Event(8, "session_update", """{"sessionUpdate":"available_commands_update"}"""),
                Event(9, "user_message", """[{"text":"A"},{"text":"B"}]"""),
                Event(10, "permission_request", "null"),
                Event(11, "permission_request", """{"requestId":"req-2","request":{"title":"Edit?","options":[{"optionId":"allow","name":"Allow","kind":"allow_once"}]}}"""),
            ]
        };
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));

        await view.LoadAsync("ws-1");
        await WaitUntilAsync(() => view.HasPermission && view.Messages.Any(item => item.Role == "Thought"));

        view.Message = "should-not-send";
        await view.SendCommand.Execute().FirstAsync();
        await view.PermissionOptions[0].Command.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(fake.Sent, Is.Null);
            Assert.That(fake.Decided, Is.EqualTo(("ws-1", "req-2", "allow")));
            Assert.That(view.HasPermission, Is.False);
            Assert.That(view.Messages.Count(item => item.Role == "Plan"), Is.EqualTo(1));
            Assert.That(view.Messages.Any(item => item.Role == "You" && item.Text.Contains("A")), Is.True);
        });

        hang.TrySetResult();
        await view.LoadAsync(null);
    }

    [AvaloniaTest]
    public async Task Stream_reportsHttpFailuresWithoutStoppingTheLoopImmediately()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(new StreamApiFake
        {
            EventsHang = hang,
            EventsFailure = new HttpRequestException("stream down")
        })));

        await view.LoadAsync("ws-1");
        await WaitUntilAsync(() => view.Error == "stream down");

        hang.TrySetResult();
        await view.LoadAsync(null);
        Assert.That(view.Error, Is.Null);
    }

    [AvaloniaTest]
    public async Task LoadAsync_reportsHttpFailures()
    {
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(new StreamApiFake
        {
            GetFailure = new HttpRequestException("offline")
        })));

        await view.LoadAsync("ws-1");

        Assert.That(view.Error, Is.EqualTo("offline"));
    }

    [AvaloniaTest]
    public async Task SendAndSelect_roundTripThroughTheController()
    {
        var fake = new StreamApiFake();
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));
        await view.LoadAsync("ws-1");
        view.Message = "hello";

        await view.SendCommand.Execute().FirstAsync();
        await view.SelectAgentCommand.Execute("Codex").FirstAsync();
        await view.StopCommand.Execute().FirstAsync();

        Assert.That(fake.Sent, Is.EqualTo(("ws-1", "hello")));
        Assert.That(fake.Scheduled, Is.EqualTo(("ws-1", "Codex")));
        Assert.That(fake.Stopped, Is.EqualTo("ws-1"));
    }

    private static AgentEventDto Event(long sequence, string type, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new AgentEventDto(sequence, type, document.RootElement.Clone(), DateTimeOffset.UnixEpoch);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(20, timeout.Token);
        }
    }
}

internal sealed class StreamApiFake : IAgentApiProvider
{
    public Exception? GetFailure { get; set; }
    public Exception? EventsFailure { get; set; }
    public TaskCompletionSource? EventsHang { get; set; }
    public List<AgentEventDto> Events { get; init; } = [];
    public AgentSessionDto? Session { get; set; }
    public (string, string)? Sent { get; private set; }
    public (string, string)? Scheduled { get; private set; }
    public (string, string)? Authenticated { get; private set; }
    public (string, string, string)? Decided { get; private set; }
    public string? Stopped { get; private set; }

    public Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (GetFailure is not null) return Task.FromException<AgentSessionDto?>(GetFailure);
        return Task.FromResult<AgentSessionDto?>(Session ?? new AgentSessionDto(workspaceId, null, "idle", null, null, [
            new AgentDescriptorDto("Codex", true, "Codex")
        ], []));
    }

    public Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken)
    {
        Scheduled = (workspaceId, agent);
        return Task.FromResult<AgentSessionDto?>(new AgentSessionDto(workspaceId, agent, "ready", "s1", null, [
            new AgentDescriptorDto(agent, true, agent)
        ], []));
    }

    public Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken)
    {
        Sent = (workspaceId, message);
        return Task.CompletedTask;
    }

    public Task AuthenticateAsync(string workspaceId, string methodId, CancellationToken cancellationToken)
    {
        Authenticated = (workspaceId, methodId);
        return Task.CompletedTask;
    }

    public Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken)
    {
        Decided = (workspaceId, requestId, optionId);
        return Task.CompletedTask;
    }

    public Task StopAsync(string workspaceId, CancellationToken cancellationToken)
    {
        Stopped = workspaceId;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<AgentEventDto> EventsAsync(
        string workspaceId,
        long after,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (EventsFailure is not null)
            throw EventsFailure;
        foreach (var item in Events.Where(item => item.Sequence > after))
            yield return item;
        if (EventsHang is not null)
            await EventsHang.Task.WaitAsync(cancellationToken);
    }
}
