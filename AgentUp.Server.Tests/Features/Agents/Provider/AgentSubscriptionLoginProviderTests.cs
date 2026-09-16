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
                new AgentLoginInbox(),
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(challenge!.Url, Does.Contain("loginDeepControl"));
                Assert.That(challenge.Transport, Is.EqualTo(AgentLoginTransport.Poll));
                Assert.That(challenge.CanSubmitCode, Is.False, "Nothing is carried back for a polling sign-in");
            });
        }
        finally
        {
            File.Delete(script);
        }
    }

    // The regression that made Claude sign-in impossible: the CLI writes its prompt without a
    // trailing newline and then blocks, so a line-oriented reader never surfaces the link above
    // it and the sign-in hangs until it is cancelled.
    [Test]
    public async Task LoginAsync_surfacesALinkFollowedByAPromptThatNeverEndsItsLine()
    {
        var script = WriteScript(
            OperatingSystem.IsWindows()
                ? "echo Visit https://claude.ai/oauth/authorize?code=true& <nul set /p=\"Paste code here: \"& timeout /t 10 /nobreak >nul"
                : "echo 'Visit https://claude.ai/oauth/authorize?code=true'; printf 'Paste code here: '; sleep 10");
        try
        {
            var provider = CreateProvider(script, ("Agents:Claude:LoginCompletionTimeoutSeconds", "2"));
            var challenges = new List<AgentLoginChallengeDto>();

            var result = await provider.LoginAsync(
                AgentKind.Claude,
                new AgentCommand(script, []),
                "claude-login",
                value => challenges.Add(value),
                new AgentLoginInbox(),
                CancellationToken.None);

            var last = challenges[^1];
            Assert.Multiple(() =>
            {
                Assert.That(last.Url, Does.Contain("claude.ai"), "The link printed before the prompt must still reach the client");
                Assert.That(last.Transport, Is.EqualTo(AgentLoginTransport.Code));
                Assert.That(last.CanSubmitCode, Is.True, "The client has to be told the CLI is waiting for a code");
                Assert.That(result.Succeeded, Is.False, "A CLI left waiting past its deadline is a failed sign-in");
            });
        }
        finally
        {
            File.Delete(script);
        }
    }

    // The other half of that regression: there was no way to answer the prompt at all.
    [Test]
    public async Task LoginAsync_writesASubmittedCodeToTheCliStandardInput()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("The shell script form of this fixture is POSIX only.");

        var echoed = Path.Join(Path.GetTempPath(), $"agent-login-code-{Guid.NewGuid():N}.txt");
        var script = WriteScript(
            $"echo 'Visit https://claude.ai/oauth/authorize?code=true'; printf 'Paste code here: '; read given; printf '%s' \"$given\" > '{echoed}'; echo 'sk-ant-oat01-exchanged'");
        try
        {
            var provider = CreateProvider(script);
            var inbox = new AgentLoginInbox();
            var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var login = provider.LoginAsync(
                AgentKind.Claude,
                new AgentCommand(script, []),
                "claude-login",
                value =>
                {
                    if (value.CanSubmitCode) waiting.TrySetResult();
                },
                inbox,
                CancellationToken.None);

            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(20));
            inbox.TrySubmit(new AgentLoginSubmission(AgentLoginSubmissionKind.Code, "code-from-the-browser#state"));
            var result = await login.WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(File.ReadAllText(echoed), Is.EqualTo("code-from-the-browser#state"));
                Assert.That(result.ClaudeOAuthToken, Is.EqualTo("sk-ant-oat01-exchanged"));
            });
        }
        finally
        {
            File.Delete(script);
            if (File.Exists(echoed)) File.Delete(echoed);
        }
    }

    [Test]
    public async Task LoginAsync_failsWhenTheCliNeverPrintsALink()
    {
        var script = WriteScript(OperatingSystem.IsWindows() ? "timeout /t 30 /nobreak >nul" : "sleep 30");
        try
        {
            var provider = CreateProvider(script, ("Agents:Cursor:LoginChallengeTimeoutSeconds", "1"));

            var result = await provider.LoginAsync(
                AgentKind.Cursor,
                new AgentCommand(script, []),
                "cursor_login",
                _ => { },
                new AgentLoginInbox(),
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Error, Does.Contain("did not print a sign-in link"));
            });
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Test]
    public async Task LoginAsync_reportsTheLoopbackAddressTheCliIsListeningOn()
    {
        var url = "https://auth.openai.com/oauth/authorize?redirect_uri=http%3A%2F%2Flocalhost%3A1455%2Fauth%2Fcallback";
        var script = WriteScript(OperatingSystem.IsWindows() ? $"echo {url}" : $"echo '{url}'");
        try
        {
            var provider = CreateProvider(script, ("Agents:Codex:LoginTransport", "redirect"));
            AgentLoginChallengeDto? challenge = null;

            var result = await provider.LoginAsync(
                AgentKind.Codex,
                new AgentCommand(script, []),
                "chatgpt",
                value => challenge = value,
                new AgentLoginInbox(),
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(challenge!.Transport, Is.EqualTo(AgentLoginTransport.Redirect));
                Assert.That(challenge.RedirectUri, Is.EqualTo("http://localhost:1455/auth/callback"),
                    "The client needs the exact address to recognise the redirect it must hand back");
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
                new AgentLoginInbox(),
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
                new AgentLoginInbox(),
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
                    new AgentLoginInbox(),
                    timeout.Token));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Test]
    public async Task LoginAsync_returnsTheResolverErrorWhenTheLoginCliIsRejected()
    {
        var provider = CreateProvider("/bin/sh");
        var result = await provider.LoginAsync(
            AgentKind.Cursor,
            new AgentCommand("/bin/sh", []),
            "api-key",
            _ => { },
            new AgentLoginInbox(),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("subscription"));
    }

    [Test]
    public async Task LoginAsync_returnsTheStartErrorWhenTheExecutableIsMissing()
    {
        var provider = CreateProvider("/definitely-missing-agent-login-cli");
        var result = await provider.LoginAsync(
            AgentKind.Cursor,
            new AgentCommand("/definitely-missing-agent-login-cli", []),
            "cursor_login",
            _ => { },
            new AgentLoginInbox(),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("Could not start"));
    }

    private static AgentSubscriptionLoginProvider CreateProvider(string script, params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["Agents:Cursor:LoginCommand"] = script,
            ["Agents:Cursor:LoginArguments:0"] = "--no-browser",
            ["Agents:Claude:LoginCommand"] = script,
            ["Agents:Codex:LoginCommand"] = script
        };
        foreach (var (key, value) in settings)
            values[key] = value;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var home = new AgentCliHomeProvider(Directory.CreateTempSubdirectory("agent-login-home").FullName);
        var environment = new AgentProcessEnvironmentProvider(home, new EmptyClaudeStore());
        return new AgentSubscriptionLoginProvider(
            new AgentLoginCommandProvider(configuration, new AgentSubscriptionAuth()),
            new AgentLoginFlowProvider(configuration),
            new AgentLoginCallbackRelay(new SingleClientFactory(), NullLogger<AgentLoginCallbackRelay>.Instance),
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

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
