using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AcpProcessProviderTests
{
    [Test]
    public async Task CallAsync_writesJsonRpcAndMatchesResponseById()
    {
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? new[] { "/d", "/s", "/c", "set /p line=& echo {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1}}" }
            : new[] { "-c", "read line; printf '%s\\n' '{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1}}'" };
        var values = new Dictionary<string, string?> { ["Agents:Codex:Command"] = command };
        for (var index = 0; index < arguments.Length; index++) values[$"Agents:Codex:Arguments:{index}"] = arguments[index];
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), []);
        await using var provider = new AcpProcessProvider(commands, NullLogger<AcpProcessProvider>.Instance);
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        var result = await provider.CallAsync("initialize", new { protocolVersion = 1 }, CancellationToken.None);

        Assert.That(result.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task CallAsync_skipsInvalidJsonAndMatchesTheNextValidResponse()
    {
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? new[] { "/d", "/s", "/c", "set /p line=& echo not-json& echo {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1}}" }
            : new[] { "-c", "read line; printf '%s\\n' 'not-json' '{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1}}'" };
        var values = new Dictionary<string, string?> { ["Agents:Codex:Command"] = command };
        for (var index = 0; index < arguments.Length; index++) values[$"Agents:Codex:Arguments:{index}"] = arguments[index];
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), []);
        await using var provider = new AcpProcessProvider(commands, NullLogger<AcpProcessProvider>.Instance);
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        var result = await provider.CallAsync("initialize", new { protocolVersion = 1 }, CancellationToken.None);

        Assert.That(result.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task CallAsync_includesStderrWhenTheAgentExits()
    {
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? new[] { "/d", "/s", "/c", "echo boom 1>&2 & exit 1" }
            : new[] { "-c", "echo boom >&2; exit 1" };
        var values = new Dictionary<string, string?> { ["Agents:Codex:Command"] = command };
        for (var index = 0; index < arguments.Length; index++)
            values[$"Agents:Codex:Arguments:{index}"] = arguments[index];
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), []);
        await using var provider = new AcpProcessProvider(commands, NullLogger<AcpProcessProvider>.Instance);
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.CallAsync("initialize", new { protocolVersion = 1 }, CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("code 1"));
        Assert.That(exception.Message, Does.Contain("boom"));
    }

    [Test]
    public void StartAsync_failsWhenTheExecutableIsMissing()
    {
        var provider = Create("/definitely-missing/acp-agent");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("ACP executable"));
    }

    [Test]
    public async Task StartAsync_rejectsASecondStart()
    {
        await using var provider = Create(
            UnixCommand(),
            UnixArguments("cat >/dev/null"));
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None));
    }

    [Test]
    public async Task CallAsync_dispatchesNotificationsRequestsAndIgnoresUnknownResponses()
    {
        var notified = new TaskCompletionSource<(string Method, JsonElement Payload)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var provider = Create(
            UnixCommand(),
            UnixArguments(
                "read line; " +
                "printf '%s\\n' '{}' '{\"jsonrpc\":\"2.0\",\"id\":\"bad\"}' '{\"jsonrpc\":\"2.0\",\"id\":99,\"result\":{}}' " +
                "'{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"ok\":true}}' " +
                "'{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"session/request_permission\",\"params\":{\"x\":1}}'; " +
                "read reply; printf '%s\\n' '{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"ok\":true}}'; cat >/dev/null"));
        provider.Notification += (method, payload) =>
        {
            notified.TrySetResult((method, payload));
            return Task.CompletedTask;
        };
        provider.Request += (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new { outcome = "allow" }));
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        var result = await provider.CallAsync("initialize", new { protocolVersion = 1 }, CancellationToken.None);
        var notification = await notified.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("ok").GetBoolean(), Is.True);
            Assert.That(notification.Method, Is.EqualTo("session/update"));
            Assert.That(notification.Payload.GetProperty("ok").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task CallAsync_writesRequestErrorsWhenTheHandlerThrows()
    {
        await using var provider = Create(
            UnixCommand(),
            UnixArguments(
                "read line; printf '%s\\n' '{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"session/request_permission\",\"params\":{}}'; " +
                "read reply; printf '%s\\n' '{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"ok\":true}}'; cat >/dev/null"));
        provider.Request += (_, _) => throw new InvalidOperationException("denied");
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        var result = await provider.CallAsync("initialize", new { protocolVersion = 1 }, CancellationToken.None);

        Assert.That(result.GetProperty("ok").GetBoolean(), Is.True);
    }

    [Test]
    public async Task CallAsync_surfacesJsonRpcErrorsAndMissingResults()
    {
        await using var errors = Create(
            UnixCommand(),
            UnixArguments("read line; printf '%s\\n' '{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":1,\"message\":\"nope\"}}'"));
        await errors.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);
        var error = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await errors.CallAsync("initialize", new { }, CancellationToken.None));
        Assert.That(error!.Message, Does.Contain("nope"));

        await using var missing = Create(
            UnixCommand(),
            UnixArguments("read line; printf '%s\\n' '{\"jsonrpc\":\"2.0\",\"id\":1}'"));
        await missing.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);
        var missingResult = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await missing.CallAsync("initialize", new { }, CancellationToken.None));
        Assert.That(missingResult!.Message, Does.Contain("without a result"));
    }

    [Test]
    public async Task CallAsync_reportsIoFailuresWhenTheProcessExits()
    {
        await using var provider = Create(UnixCommand(), UnixArguments("exit 0"));
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);
        await Task.Delay(50);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CallAsync("initialize", new { }, CancellationToken.None));
    }

    [Test]
    public async Task NotifyAsync_writesANotificationLine()
    {
        await using var provider = Create(UnixCommand(), UnixArguments("cat >/dev/null"));
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        await provider.NotifyAsync("session/cancel", new { }, CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_killsAProcessThatIgnoresStdinClose()
    {
        await using var provider = Create(UnixCommand(), UnixArguments("sleep 30"));
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        await provider.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task CallAsync_trimsExcessStderrBeforeReportingExit()
    {
        await using var provider = Create(
            UnixCommand(),
            UnixArguments("i=1; while [ \"$i\" -le 20 ]; do echo err$i >&2; i=$((i+1)); done; exit 1"));
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CallAsync("initialize", new { }, CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("err20"));
        Assert.That(exception.Message, Does.Not.Contain("err1 "));
    }

    private static AcpProcessProvider Create(string command, params string[] arguments)
    {
        var values = new Dictionary<string, string?> { ["Agents:Codex:Command"] = command };
        for (var index = 0; index < arguments.Length; index++)
            values[$"Agents:Codex:Arguments:{index}"] = arguments[index];
        return new AcpProcessProvider(
            new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), []),
            NullLogger<AcpProcessProvider>.Instance);
    }

    private static string UnixCommand() => OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";

    private static string[] UnixArguments(string script) =>
        OperatingSystem.IsWindows()
            ? ["/d", "/s", "/c", script]
            : ["-c", script];
}
