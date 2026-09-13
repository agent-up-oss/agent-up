using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSubscriptionLoginProviderTests
{
    [Test]
    public async Task LoginAsync_publishesThePrintedLinkThenSucceeds()
    {
        var script = WriteScript(
            OperatingSystem.IsWindows()
                ? "echo Open https://cursor.com/loginDeepControl?challenge=abc"
                : "echo 'Open https://cursor.com/loginDeepControl?challenge=abc'");
        try
        {
            var provider = CreateProvider(script);
            AgentLoginChallengeDto? challenge = null;

            var result = await provider.LoginAsync(
                AgentKind.Cursor,
                new AgentCommand(script, []),
                "cursor_login",
                value => challenge = value,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(challenge!.Url, Does.Contain("loginDeepControl"));
            });
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Test]
    public async Task LoginAsync_returnsTheExitErrorWhenTheCliFails()
    {
        var script = WriteScript(OperatingSystem.IsWindows() ? "echo failed 1>&2 & exit 7" : "echo failed >&2; exit 7");
        try
        {
            var provider = CreateProvider(script);
            var result = await provider.LoginAsync(
                AgentKind.Cursor,
                new AgentCommand(script, []),
                "cursor_login",
                _ => { },
                CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("exit code 7"));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Test]
    public async Task LoginAsync_returnsTheClaudeSubscriptionToken()
    {
        var script = WriteScript(
            OperatingSystem.IsWindows()
                ? "echo sk-ant-oat01-from-cli"
                : "echo 'sk-ant-oat01-from-cli'");
        try
        {
            var provider = CreateProvider(script);

            var result = await provider.LoginAsync(
                AgentKind.Claude,
                new AgentCommand(script, []),
                "claude-login",
                _ => { },
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.ClaudeOAuthToken, Is.EqualTo("sk-ant-oat01-from-cli"));
            });
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Test]
    public void LoginAsync_stopsTheCliWhenCancelled()
    {
        var script = WriteScript(OperatingSystem.IsWindows() ? "timeout /t 30 /nobreak >nul" : "sleep 30");
        try
        {
            var provider = CreateProvider(script);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            Assert.CatchAsync<OperationCanceledException>(async () => await provider.LoginAsync(
                    AgentKind.Cursor,
                    new AgentCommand(script, []),
                    "cursor_login",
                    _ => { },
                    timeout.Token));
        }
        finally
        {
            File.Delete(script);
        }
    }

    private static AgentSubscriptionLoginProvider CreateProvider(string script)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agents:Cursor:LoginCommand"] = script,
            ["Agents:Claude:LoginCommand"] = script
        }).Build();
        var home = new AgentCliHomeProvider(Directory.CreateTempSubdirectory("agent-login-home").FullName);
        var environment = new AgentProcessEnvironmentProvider(home, new EmptyClaudeStore());
        return new AgentSubscriptionLoginProvider(
            new AgentLoginCommandProvider(configuration, new AgentSubscriptionAuth()),
            environment,
            NullLogger<AgentSubscriptionLoginProvider>.Instance);
    }

    private static string WriteScript(string body)
    {
        if (OperatingSystem.IsWindows())
        {
            var file = Path.Join(Path.GetTempPath(), $"agent-login-{Guid.NewGuid():N}.cmd");
            File.WriteAllText(file, $"@echo off\r\n{body}\r\n");
            return file;
        }

        var script = Path.Join(Path.GetTempPath(), $"agent-login-{Guid.NewGuid():N}.sh");
        File.WriteAllText(script, $"#!/bin/sh\n{body}\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return script;
    }

    private sealed class EmptyClaudeStore : IAgentClaudeCredentialStore
    {
        public string? Read() => null;
        public void Write(string token) { }
    }
}
