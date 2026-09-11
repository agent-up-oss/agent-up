using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentSchedulingServiceTests
{
    private AgentSchedulingService _service = null!;
    private FakeAgentProcessProvider _process = null!;
    private AgentEventFrameProvider _payloads = null!;
    private AgentEventService _events = null!;
    private Workspace _workspace = null!;
    private WorkspaceRegistry _registry = null!;

    [SetUp]
    public async Task SetUp()
    {
        _registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService([])),
            new WorkspaceEventBus());
        await _registry.StartAsync(CancellationToken.None);
        _workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("Workspace", "/repo", "/repo", "main", "abc"));
        _process = new FakeAgentProcessProvider();
        _payloads = new AgentEventFrameProvider();
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Agents:Codex:Command"] = command }).Build(), []);
        _events = new AgentEventService(_payloads);
        _service = new AgentSchedulingService(
            new WorkspaceQueryController(_registry), new FakeAgentProcessFactory(_process), commands,
            _payloads, _events, NullLogger<AgentSchedulingService>.Instance);
    }

    [TearDown]
    public async Task TearDown() => await _service.DisposeAsync();

    [Test]
    public async Task Schedule_initializesAcpInWorktreeAndEnforcesOneSession()
    {
        var first = await _service.ScheduleAsync(_workspace.Id, AgentKind.Codex, CancellationToken.None);
        var second = await _service.ScheduleAsync(_workspace.Id, AgentKind.Codex, CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(first.Session!.SessionId, Is.EqualTo("session-1"));
            Assert.That(_process.WorkingDirectory, Is.EqualTo("/repo"));
            Assert.That(_process.Methods, Is.EqualTo(new[] { "initialize", "session/new" }));
            Assert.That(second.Error, Does.Contain("already has an agent"));
        });
    }

    [Test]
    public async Task Prompt_isAcceptedImmediatelyAndConcurrentPromptIsRejected()
    {
        await _service.ScheduleAsync(_workspace.Id, AgentKind.Codex, CancellationToken.None);
        _process.HoldPrompt = true;

        var first = await _service.PromptAsync(_workspace.Id, "first", CancellationToken.None);
        var second = await _service.PromptAsync(_workspace.Id, "second", CancellationToken.None);

        Assert.Multiple(() => { Assert.That(first.Error, Is.Null); Assert.That(second.Error, Does.Contain("already processing")); });
        _process.CompletePrompt();
    }

    [Test]
    public async Task Authenticate_acceptsOnlyAdvertisedMethodAndCreatesSession()
    {
        _process.RequireAuthentication = true;
        var scheduled = await _service.ScheduleAsync(_workspace.Id, AgentKind.Codex, CancellationToken.None);

        var invalid = _service.Authenticate(_workspace.Id, "unknown");
        var accepted = _service.Authenticate(_workspace.Id, "chatgpt");
        await WaitForStateAsync("ready");

        Assert.Multiple(() => {
            Assert.That(scheduled.Session!.State, Is.EqualTo("authentication_required"));
            Assert.That(scheduled.Session.AuthMethods.Single().Name, Is.EqualTo("ChatGPT subscription"));
            Assert.That(invalid.Error, Does.Contain("not offered"));
            Assert.That(accepted.Error, Is.Null);
            Assert.That(_service.Get(_workspace.Id)!.SessionId, Is.EqualTo("session-1"));
        });
    }

    [Test]
    public async Task PermissionDecision_returnsSelectedAcpOutcome()
    {
        await _service.ScheduleAsync(_workspace.Id, AgentKind.Codex, CancellationToken.None);
        var requestTask = _process.RequestPermissionAsync(_payloads.Payload(new { options = new[] { new { optionId = "allow" } } }));
        var requestId = await ReadPermissionRequestIdAsync();

        var invalid = _service.Decide(_workspace.Id, new AgentPermissionResponse(requestId, "not-offered"));
        var decided = _service.Decide(_workspace.Id, new AgentPermissionResponse(requestId, "allow"));
        var result = await requestTask;

        Assert.Multiple(() => { Assert.That(invalid, Is.False); Assert.That(decided, Is.True); Assert.That(result.GetProperty("outcome").GetProperty("optionId").GetString(), Is.EqualTo("allow")); });
    }

    [Test]
    public async Task SessionUpdate_isForwardedWithoutChangingPayload()
    {
        await _service.ScheduleAsync(_workspace.Id, AgentKind.Codex, CancellationToken.None);
        await _process.SendNotificationAsync("session/update", _payloads.Payload(new { update = new { sessionUpdate = "agent_message_chunk", content = new { type = "text", text = "hello" } } }));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var stream = _events.SubscribeAsync(_workspace.Id, 0, timeout.Token).GetAsyncEnumerator(timeout.Token);
        await stream.MoveNextAsync();
        await stream.MoveNextAsync();
        Assert.That(await stream.MoveNextAsync(), Is.True);
        Assert.That(stream.Current.Payload.GetProperty("update").GetProperty("content").GetProperty("text").GetString(), Is.EqualTo("hello"));
    }

    [Test]
    public async Task RemovingWorkspace_stopsItsScheduledAgent()
    {
        await _service.ScheduleAsync(_workspace.Id, AgentKind.Codex, CancellationToken.None);
        await _registry.RemoveAsync(_workspace.Id);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (_process.StopCalls == 0) await Task.Delay(10, timeout.Token);

        Assert.That(_process.StopCalls, Is.EqualTo(1));
    }

    private async Task<string> ReadPermissionRequestIdAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var stream = _events.SubscribeAsync(_workspace.Id, 0, timeout.Token).GetAsyncEnumerator(timeout.Token);
        await stream.MoveNextAsync();
        await stream.MoveNextAsync();
        if (!await stream.MoveNextAsync()) throw new InvalidOperationException("Permission request event was not published.");
        return stream.Current.Payload.GetProperty("requestId").GetString()!;
    }

    private async Task WaitForStateAsync(string expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (_service.Get(_workspace.Id)!.State != expected) await Task.Delay(10, timeout.Token);
    }
}

internal sealed class FakeAgentProcessFactory(IAgentProcessProvider process) : IAgentProcessFactory
{
    public IAgentProcessProvider Create() => process;
}

internal sealed class FakeAgentProcessProvider : IAgentProcessProvider
{
    private TaskCompletionSource<JsonElement>? _prompt;
    public event Func<string, JsonElement, Task>? Notification;
    public event Func<string, JsonElement, Task<JsonElement>>? Request;
    public event Action<string?>? Exited;
    public List<string> Methods { get; } = [];
    public string? WorkingDirectory { get; private set; }
    public bool HoldPrompt { get; set; }
    public bool RequireAuthentication { get; set; }
    public bool Authenticated { get; private set; }
    public int StopCalls { get; private set; }

    public Task StartAsync(AgentKind kind, string workingDirectory, CancellationToken cancellationToken) { WorkingDirectory = workingDirectory; return Task.CompletedTask; }
    public async Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        Methods.Add(method);
        if (method == "authenticate") { Authenticated = true; return JsonSerializer.SerializeToElement(new { }); }
        if (method == "session/new" && RequireAuthentication && !Authenticated) throw new InvalidOperationException("Authentication required.");
        if (method == "session/prompt" && HoldPrompt)
        {
            _prompt = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return await _prompt.Task.WaitAsync(cancellationToken);
        }
        if (method == "session/new") return JsonSerializer.SerializeToElement(new { sessionId = "session-1" });
        return RequireAuthentication
            ? JsonSerializer.SerializeToElement(new { protocolVersion = 1, authMethods = new[] { new { id = "chatgpt", name = "ChatGPT subscription", description = "Use an existing subscription." } } })
            : JsonSerializer.SerializeToElement(new { protocolVersion = 1 });
    }
    public Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) { StopCalls++; return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void CompletePrompt() => _prompt!.TrySetResult(JsonSerializer.SerializeToElement(new { stopReason = "end_turn" }));
    public Task SendNotificationAsync(string method, JsonElement payload) => Notification?.Invoke(method, payload) ?? Task.CompletedTask;
    public void Exit(string? error) => Exited?.Invoke(error);
    public async Task<JsonElement> RequestPermissionAsync(JsonElement payload)
    {
        var handler = Request ?? throw new InvalidOperationException("Permission request handler was not registered.");
        return await handler("session/request_permission", payload);
    }
}
