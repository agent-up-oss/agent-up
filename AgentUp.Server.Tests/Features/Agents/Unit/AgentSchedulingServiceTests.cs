using System.Text.Json;
using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentSchedulingServiceTests
{
    private AgentSchedulingService _service = null!;
    private FakeAgentProcessProvider _process = null!;
    private FakeSubscriptionLoginProvider _login = null!;
    private FakeClaudeCredentialStore _credentials = null!;
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
            new CapabilitiesController(new CapabilityReconciliationService()),
            new WorkspaceEventBus());
        await _registry.StartAsync(CancellationToken.None);
        _workspace = await _registry.RegisterAsync(ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
        _process = new FakeAgentProcessProvider();
        _payloads = new AgentEventFrameProvider();
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Agents:Codex:Command"] = command,
                ["Agents:Cursor:Command"] = command,
                ["Agents:Claude:Command"] = command
            }).Build());
        _events = new AgentEventService(_payloads);
        _login = new FakeSubscriptionLoginProvider();
        _credentials = new FakeClaudeCredentialStore();
        _service = new AgentSchedulingService(
            new WorkspaceQueryController(_registry), new FakeAgentProcessFactory(_process), commands,
            _login, new FakeProcessEnvironmentProvider(), _credentials, new AgentSubscriptionAuth(),
            _payloads, _events, NullLogger<AgentSchedulingService>.Instance);
    }

    [TearDown]
    public async Task TearDown() => await _service.DisposeAsync();

    [Test]
    public async Task GetAsync_lists_agents_from_enabled_modules()
    {
        var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Codex())
            .WithAgents(new StubAgentCapability());
        var service = new AgentSchedulingService(
            new WorkspaceQueryController(_registry), new FakeAgentProcessFactory(_process),
            new AgentCommandProvider(new ConfigurationBuilder().Build(), packages),
            _login, new FakeProcessEnvironmentProvider(), _credentials, new AgentSubscriptionAuth(),
            _payloads, _events, NullLogger<AgentSchedulingService>.Instance, packages);
        try
        {
            var session = await service.GetAsync(_workspace.Id, CancellationToken.None);

            Assert.That(session!.Agents.Select(agent => agent.Agent), Is.EqualTo(new[] { "codex" }));
            Assert.That(session.Agents.Single().Available, Is.True);
            Assert.That(session.Agents.Single().DisplayName, Is.EqualTo("Codex"));
        }
        finally
        {
            await service.DisposeAsync();
        }
    }

    [Test]
    public async Task GetAsync_lists_unknown_agent_module_ids()
    {
        var packages = new FakeEnabledCapabilityPackages(new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityPackageManifest
        {
            Id = "windsurf",
            Version = "1.0.0",
            DisplayName = "Windsurf",
            Kind = "agent",
            Launch = new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityLaunchTemplate
            {
                Command = "windsurf-acp",
                Arguments = []
            }
        }).WithAgents(new StubAgentCapability { Identity = new("windsurf", "1.0.0", "Windsurf", "agent-up") });
        var service = new AgentSchedulingService(
            new WorkspaceQueryController(_registry), new FakeAgentProcessFactory(_process),
            new AgentCommandProvider(new ConfigurationBuilder().Build(), packages),
            _login, new FakeProcessEnvironmentProvider(), _credentials, new AgentSubscriptionAuth(),
            _payloads, _events, NullLogger<AgentSchedulingService>.Instance, packages);
        try
        {
            var session = await service.GetAsync(_workspace.Id, CancellationToken.None);

            Assert.That(session!.Agents.Select(agent => agent.Agent), Is.EqualTo(new[] { "windsurf" }));
            Assert.That(session.Agents.Single().DisplayName, Is.EqualTo("Windsurf"));
        }
        finally
        {
            await service.DisposeAsync();
        }
    }

    [Test]
    public async Task Schedule_initializesAcpInWorktreeAndEnforcesOneSession()
    {
        var first = await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        var second = await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(first.Session!.SessionId, Is.EqualTo("session-1"));
            Assert.That(_process.WorkingDirectory, Is.EqualTo("/repo"));
            Assert.That(_process.Environment!["HOME"], Is.EqualTo("/data/agent-cli-home"));
            Assert.That(_process.Methods, Is.EqualTo(new[] { "initialize", "session/new" }));
            Assert.That(second.Error, Does.Contain("already has an agent"));
        });
    }

    [Test]
    [TestCaseSource(nameof(MissingSessionIds))]
    public async Task Schedule_disposesTheProcessWhenSessionIdIsMissing(object payload)
    {
        _process.SessionNewResult = JsonSerializer.SerializeToElement(payload);

        var result = await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Does.Contain("session ID"));
            Assert.That(_process.DisposeCalls, Is.EqualTo(1));
            Assert.That(_service.Get(_workspace.Id)!.SessionId, Is.Null);
            Assert.That(_service.Get(_workspace.Id)!.State, Is.EqualTo("idle"));
        });
    }

    [Test]
    public async Task Schedule_doesNotTreatAMissingSessionIdAsAuthentication()
    {
        _process.AdvertiseAuthMethods = true;
        _process.SessionNewResult = JsonSerializer.SerializeToElement(new { });

        var result = await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Does.Contain("session ID"));
            Assert.That(_process.DisposeCalls, Is.EqualTo(1));
            Assert.That(_service.Get(_workspace.Id)!.State, Is.EqualTo("idle"));
        });
    }

    private static object[] MissingSessionIds =>
    [
        new object(),
        new { sessionId = 12 },
        new { sessionId = "  " }
    ];

    [Test]
    public async Task Prompt_isAcceptedImmediatelyAndConcurrentPromptIsRejected()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        _process.HoldPrompt = true;

        var first = await _service.PromptAsync(_workspace.Id, "first", CancellationToken.None);
        var second = await _service.PromptAsync(_workspace.Id, "second", CancellationToken.None);

        Assert.Multiple(() => { Assert.That(first.Error, Is.Null); Assert.That(second.Error, Does.Contain("already processing")); });
        _process.CompletePrompt();
    }

    [Test]
    public async Task Prompt_isRejectedUntilTheAcpSessionIsReady()
    {
        _process.RequireAuthentication = true;
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        var result = await _service.PromptAsync(_workspace.Id, "hello", CancellationToken.None);

        Assert.That(result.Error, Does.Contain("not ready"));
    }

    [Test]
    public async Task Cancel_isRejectedWhenTheAcpSessionHasNotBeenCreated()
    {
        _process.RequireAuthentication = true;
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        var result = await _service.CancelAsync(_workspace.Id, CancellationToken.None);

        Assert.That(result.Error, Does.Contain("no ACP session"));
    }

    [Test]
    public async Task Authenticate_acceptsOnlyAdvertisedMethodAndCreatesSession()
    {
        _process.RequireAuthentication = true;
        var scheduled = await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        var invalid = _service.Authenticate(_workspace.Id, "unknown");
        var accepted = _service.Authenticate(_workspace.Id, "chatgpt");
        await WaitForStateAsync("ready");

        Assert.Multiple(() => {
            Assert.That(scheduled.Session!.State, Is.EqualTo("authentication_required"));
            Assert.That(scheduled.Session.AuthMethods.Single().Name, Is.EqualTo("ChatGPT subscription"));
            Assert.That(invalid.Error, Does.Contain("not offered"));
            Assert.That(accepted.Error, Is.Null);
            Assert.That(_login.MethodId, Is.EqualTo("chatgpt"));
            Assert.That(_process.Methods, Does.Not.Contain("authenticate"));
            Assert.That(_service.Get(_workspace.Id)!.SessionId, Is.EqualTo("session-1"));
        });
    }

    [Test]
    public async Task PermissionDecision_returnsSelectedAcpOutcome()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
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
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
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
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        await _registry.RemoveAsync(_workspace.Id);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (_process.StopCalls == 0) await Task.Delay(10, timeout.Token);

        Assert.That(_process.StopCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Prompt_reachesReadyAfterTheAgentFinishes()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        _process.HoldPrompt = true;

        Assert.That((await _service.PromptAsync(_workspace.Id, "hello", CancellationToken.None)).Error, Is.Null);
        _process.CompletePrompt();
        await WaitForStateAsync("ready");

        Assert.That(_service.Get(_workspace.Id)!.Error, Is.Null);
    }

    [Test]
    public async Task Prompt_reportsAgentFailuresWithoutLeavingTheSessionBusy()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        _process.PromptFailure = new InvalidOperationException("prompt exploded");

        Assert.That((await _service.PromptAsync(_workspace.Id, "hello", CancellationToken.None)).Error, Is.Null);
        await WaitForStateAsync("ready");

        Assert.That(_service.Get(_workspace.Id)!.Error, Does.Contain("prompt exploded"));
        Assert.That((await _service.PromptAsync(_workspace.Id, "again", CancellationToken.None)).Error, Is.Null);
    }

    [Test]
    public async Task Cancel_succeedsWhenTheSessionIsNotRunning()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        var result = await _service.CancelAsync(_workspace.Id, CancellationToken.None);

        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public async Task Cancel_notifiesTheRunningPrompt()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        _process.HoldPrompt = true;
        Assert.That((await _service.PromptAsync(_workspace.Id, "hello", CancellationToken.None)).Error, Is.Null);

        var result = await _service.CancelAsync(_workspace.Id, CancellationToken.None);

        Assert.That(result.Error, Is.Null);
        Assert.That(_process.Notifications, Does.Contain("session/cancel"));
        Assert.That(_service.Get(_workspace.Id)!.State, Is.EqualTo("ready"));
        _process.CompletePrompt();
    }

    [Test]
    public async Task Cancel_reportsNotifyFailures()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        _process.HoldPrompt = true;
        _process.NotifyFailure = new InvalidOperationException("cancel rejected");
        Assert.That((await _service.PromptAsync(_workspace.Id, "hello", CancellationToken.None)).Error, Is.Null);

        var result = await _service.CancelAsync(_workspace.Id, CancellationToken.None);

        Assert.That(result.Error, Does.Contain("cancel rejected"));
        _process.CompletePrompt();
    }

    [Test]
    public async Task Authenticate_returnsToAuthenticationRequiredWhenTheAgentFails()
    {
        _process.RequireAuthentication = true;
        _login.Succeeded = false;
        _login.Error = "auth failed";
        _login.Challenge = new AgentLoginChallengeDto(
            "https://cursor.com/loginDeepControl?challenge=abc",
            null,
            "Open this link and sign in with your subscription.");
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        var accepted = _service.Authenticate(_workspace.Id, "chatgpt");
        await WaitForStateAsync("authentication_required");

        Assert.That(accepted.Error, Is.Null);
        Assert.That(_service.Get(_workspace.Id)!.Error, Does.Contain("auth failed"));
        Assert.That(_service.Get(_workspace.Id)!.LoginChallenge!.Url, Does.Contain("loginDeepControl"));
    }

    [Test]
    public async Task Authenticate_rejectsApiKeyMethods()
    {
        _process.RequireAuthentication = true;
        _process.AdvertiseApiKey = true;
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        var rejected = _service.Authenticate(_workspace.Id, "api-key");

        Assert.That(rejected.Error, Does.Contain("not offered"));
        Assert.That(_service.Get(_workspace.Id)!.AuthMethods.Select(method => method.Id), Does.Not.Contain("api-key"));
    }

    [Test]
    public async Task Authenticate_publishesTheSubscriptionLoginLinkWhileWaiting()
    {
        _process.RequireAuthentication = true;
        _login.Hold = true;
        _login.Challenge = new AgentLoginChallengeDto(
            "https://cursor.com/loginDeepControl?challenge=abc",
            null,
            "Open this link and sign in with your subscription.");
        await _service.ScheduleAsync(_workspace.Id, "cursor", CancellationToken.None);

        var accepted = _service.Authenticate(_workspace.Id, "chatgpt");
        await WaitUntilAsync(() => _service.Get(_workspace.Id)!.LoginChallenge?.Url is not null);

        Assert.Multiple(() =>
        {
            Assert.That(accepted.Error, Is.Null);
            Assert.That(_service.Get(_workspace.Id)!.State, Is.EqualTo("authenticating"));
            Assert.That(_service.Get(_workspace.Id)!.LoginChallenge!.Url, Does.Contain("loginDeepControl"));
        });

        _login.Release();
        await WaitForStateAsync("ready");
        Assert.That(_service.Get(_workspace.Id)!.LoginChallenge, Is.Null);
    }

    [Test]
    public async Task SubmitLoginCode_reachesTheSignInThatIsWaitingForIt()
    {
        _process.RequireAuthentication = true;
        _login.Hold = true;
        _login.Challenge = new AgentLoginChallengeDto(
            "https://claude.ai/oauth/authorize?code=true",
            null,
            "Open this link, sign in, then paste the code it gives you back here.",
            AgentLoginTransport.Code,
            CanSubmitCode: true);
        await _service.ScheduleAsync(_workspace.Id, "claude", CancellationToken.None);
        // The fake agent advertises the chatgpt method id for every kind; Authenticate validates
        // the id against what was advertised, not against the kind.
        var started = _service.Authenticate(_workspace.Id, "chatgpt");
        Assert.That(started.Error, Is.Null);
        await WaitForStateAsync("authenticating");

        var accepted = _service.SubmitLoginCode(_workspace.Id, "code-from-the-browser#state");
        await WaitUntilAsync(() => _login.Submissions.Count > 0);

        Assert.Multiple(() =>
        {
            Assert.That(accepted.Error, Is.Null);
            Assert.That(_login.Submissions[0].Kind, Is.EqualTo(AgentLoginSubmissionKind.Code));
            Assert.That(_login.Submissions[0].Value, Is.EqualTo("code-from-the-browser#state"));
        });

        _login.Release();
        await WaitForStateAsync("ready");
    }

    [Test]
    public async Task SubmitLoginCallback_reachesTheSignInThatIsWaitingForIt()
    {
        _process.RequireAuthentication = true;
        _login.Hold = true;
        _login.Challenge = new AgentLoginChallengeDto(
            "https://auth.openai.com/oauth/authorize",
            null,
            "Open this link and sign in.",
            AgentLoginTransport.Redirect,
            RedirectUri: "http://localhost:1455/auth/callback");
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        _service.Authenticate(_workspace.Id, "chatgpt");
        await WaitForStateAsync("authenticating");

        var accepted = _service.SubmitLoginCallback(_workspace.Id, "http://localhost:1455/auth/callback?code=abc");
        await WaitUntilAsync(() => _login.Submissions.Count > 0);

        Assert.Multiple(() =>
        {
            Assert.That(accepted.Error, Is.Null);
            Assert.That(_login.Submissions[0].Kind, Is.EqualTo(AgentLoginSubmissionKind.Callback));
            Assert.That(_login.Submissions[0].Value, Does.Contain("code=abc"));
        });

        _login.Release();
        await WaitForStateAsync("ready");
    }

    [Test]
    public async Task SubmitLoginCode_isRefusedWhenNoSignInIsRunning()
    {
        _process.RequireAuthentication = true;
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        var refused = _service.SubmitLoginCode(_workspace.Id, "ABCD-EFGHI");

        Assert.That(refused.Error, Does.Contain("not signing in"));
    }

    [Test]
    public void SubmitLoginCode_isNotFoundForAWorkspaceWithNoAgent()
    {
        Assert.That(_service.SubmitLoginCode(_workspace.Id, "ABCD-EFGHI").Found, Is.False);
    }

    [Test]
    public async Task Schedule_offersClaudeSubscriptionLoginWhenInitializeOmitsMethods()
    {
        _process.RequireAuthentication = true;
        _process.OmitAuthMethods = true;
        var scheduled = await _service.ScheduleAsync(_workspace.Id, "claude", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(scheduled.Session!.State, Is.EqualTo("authentication_required"));
            Assert.That(scheduled.Session.AuthMethods.Single().Id, Is.EqualTo("claude-login"));
            Assert.That(scheduled.Session.AuthMethods.Single().Name, Is.EqualTo("Claude Pro"));
        });
    }

    [Test]
    public async Task Authenticate_storesTheClaudeSubscriptionToken()
    {
        _process.RequireAuthentication = true;
        _process.OmitAuthMethods = true;
        _login.ClaudeToken = "sk-ant-oat-test-token";
        await _service.ScheduleAsync(_workspace.Id, "claude", CancellationToken.None);

        Assert.That(_service.Authenticate(_workspace.Id, "claude-login").Error, Is.Null);
        await WaitForStateAsync("ready");

        Assert.That(_credentials.Token, Is.EqualTo("sk-ant-oat-test-token"));
        Assert.That(_process.StartCalls, Is.EqualTo(2));
    }

    [Test]
    public async Task Authenticate_ignoresStopFailuresWhenRestartingAfterLogin()
    {
        _process.RequireAuthentication = true;
        _process.StopFailure = new InvalidOperationException("could not stop");
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        Assert.That(_service.Authenticate(_workspace.Id, "chatgpt").Error, Is.Null);
        await WaitForStateAsync("ready");

        Assert.That(_process.StartCalls, Is.EqualTo(2));
    }

    [Test]
    public async Task Authenticate_returnsToAuthenticationRequiredWhenLoginThrows()
    {
        _process.RequireAuthentication = true;
        _login.Failure = new IOException("disk");
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        Assert.That(_service.Authenticate(_workspace.Id, "chatgpt").Error, Is.Null);
        await WaitForStateAsync("authentication_required");

        Assert.That(_service.Get(_workspace.Id)!.Error, Does.Contain("disk"));
    }

    [Test]
    public async Task Authenticate_stopsWaitingWhenTheSessionIsCancelled()
    {
        _process.RequireAuthentication = true;
        _login.Hold = true;
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        Assert.That(_service.Authenticate(_workspace.Id, "chatgpt").Error, Is.Null);
        await WaitUntilAsync(() => _service.Get(_workspace.Id)!.State == "authenticating");

        Assert.That((await _service.StopAsync(_workspace.Id, CancellationToken.None)).Error, Is.Null);
        Assert.That(_service.Get(_workspace.Id)!.State, Is.EqualTo("idle"));
    }

    [Test]
    public async Task Schedule_rejectsAnUnsupportedProtocolVersion()
    {
        _process.ProtocolVersion = 2;

        var result = await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        Assert.That(result.Error, Does.Contain("protocol version"));
    }

    [Test]
    public async Task ProcessExit_marksTheSessionStopped()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);

        _process.Exit("killed");
        await WaitForStateAsync("stopped");

        Assert.That(_service.Get(_workspace.Id)!.Error, Is.EqualTo("killed"));
    }

    [Test]
    public async Task Stop_reportsProcessStopFailures()
    {
        await _service.ScheduleAsync(_workspace.Id, "codex", CancellationToken.None);
        _process.StopFailure = new InvalidOperationException("could not stop");

        var result = await _service.StopAsync(_workspace.Id, CancellationToken.None);

        Assert.That(result.Error, Does.Contain("could not stop"));
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

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (!condition()) await Task.Delay(10, timeout.Token);
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
    public List<string> Notifications { get; } = [];
    public string? WorkingDirectory { get; private set; }
    public IReadOnlyDictionary<string, string>? Environment { get; private set; }
    public int StartCalls { get; private set; }
    public bool HoldPrompt { get; set; }
    public bool RequireAuthentication { get; set; }
    public bool AdvertiseAuthMethods { get; set; }
    public bool AdvertiseApiKey { get; set; }
    public bool OmitAuthMethods { get; set; }
    public bool Authenticated { get; private set; }
    public int StopCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public JsonElement? SessionNewResult { get; set; }
    public int ProtocolVersion { get; set; } = 1;
    public Exception? AuthenticateFailure { get; set; }
    public Exception? PromptFailure { get; set; }
    public Exception? NotifyFailure { get; set; }
    public Exception? StopFailure { get; set; }

    public Task StartAsync(
        string agent,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        StartCalls++;
        WorkingDirectory = workingDirectory;
        Environment = environment;
        return Task.CompletedTask;
    }
    public async Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        Methods.Add(method);
        if (method == "authenticate")
        {
            if (AuthenticateFailure is not null) throw AuthenticateFailure;
            Authenticated = true;
            return JsonSerializer.SerializeToElement(new { });
        }
        if (method == "session/new" && RequireAuthentication && !Authenticated && StartCalls < 2) throw new InvalidOperationException("Authentication required.");
        if (method == "session/prompt")
        {
            if (PromptFailure is not null) throw PromptFailure;
            if (HoldPrompt)
            {
                _prompt = new(TaskCreationOptions.RunContinuationsAsynchronously);
                return await _prompt.Task.WaitAsync(cancellationToken);
            }
        }
        if (method == "session/new")
            return SessionNewResult ?? JsonSerializer.SerializeToElement(new { sessionId = "session-1" });
        return AuthMethodsPayload();
    }

    private JsonElement AuthMethodsPayload()
    {
        if (OmitAuthMethods)
            return JsonSerializer.SerializeToElement(new { protocolVersion = ProtocolVersion });
        if (AdvertiseApiKey)
            return JsonSerializer.SerializeToElement(new { protocolVersion = ProtocolVersion, authMethods = new[] { new { id = "api-key", name = "API Key", description = "Use an API key to authenticate" } } });
        if (RequireAuthentication || AdvertiseAuthMethods)
            return JsonSerializer.SerializeToElement(new { protocolVersion = ProtocolVersion, authMethods = new[] { new { id = "chatgpt", name = "ChatGPT subscription", description = "Use an existing subscription." } } });
        return JsonSerializer.SerializeToElement(new { protocolVersion = ProtocolVersion });
    }

    public Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        Notifications.Add(method);
        return NotifyFailure is null ? Task.CompletedTask : Task.FromException(NotifyFailure);
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCalls++;
        return StopFailure is null ? Task.CompletedTask : Task.FromException(StopFailure);
    }
    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        return ValueTask.CompletedTask;
    }
    public void CompletePrompt() => _prompt!.TrySetResult(JsonSerializer.SerializeToElement(new { stopReason = "end_turn" }));
    public Task SendNotificationAsync(string method, JsonElement payload) => Notification?.Invoke(method, payload) ?? Task.CompletedTask;
    public void Exit(string? error) => Exited?.Invoke(error);
    public async Task<JsonElement> RequestPermissionAsync(JsonElement payload)
    {
        var handler = Request ?? throw new InvalidOperationException("Permission request handler was not registered.");
        return await handler("session/request_permission", payload);
    }
}

internal sealed class FakeSubscriptionLoginProvider : IAgentSubscriptionLoginProvider
{
    private TaskCompletionSource? _hold;

    public string? MethodId { get; private set; }
    public bool Hold { get; set; }
    public bool Succeeded { get; set; } = true;
    public string? Error { get; set; }
    public string? ClaudeToken { get; set; }
    public AgentLoginChallengeDto? Challenge { get; set; }
    public Exception? Failure { get; set; }
    public List<AgentLoginSubmission> Submissions { get; } = [];

    public async Task<AgentSubscriptionLoginResult> LoginAsync(
        string agent,
        AgentCommand acpCommand,
        string methodId,
        Action<AgentLoginChallengeDto> onChallenge,
        AgentLoginInbox inbox,
        CancellationToken cancellationToken)
    {
        MethodId = methodId;
        if (Failure is not null)
            throw Failure;
        if (Challenge is not null)
            onChallenge(Challenge);
        if (Hold)
        {
            _hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var draining = DrainAsync(inbox, cancellationToken);
            await _hold.Task.WaitAsync(cancellationToken);
            inbox.Complete();
            await draining;
        }

        if (!Succeeded)
            return AgentSubscriptionLoginResult.Failed(Error ?? "auth failed", Challenge);
        return AgentSubscriptionLoginResult.SucceededResult(
            Challenge ?? new AgentLoginChallengeDto(null, null, null),
            ClaudeToken);
    }

    public void Release() => _hold?.TrySetResult();

    private async Task DrainAsync(AgentLoginInbox inbox, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var submission in inbox.ReadAllAsync(cancellationToken))
                Submissions.Add(submission);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
}

internal sealed class FakeProcessEnvironmentProvider : IAgentProcessEnvironmentProvider
{
    public IReadOnlyDictionary<string, string> EnvironmentFor(string agent) =>
        new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = "/data/agent-cli-home" };
}

internal sealed class FakeClaudeCredentialStore : IAgentClaudeCredentialStore
{
    public string? Token { get; private set; }
    public string? Read() => Token;
    public void Write(string token) => Token = token;
}
