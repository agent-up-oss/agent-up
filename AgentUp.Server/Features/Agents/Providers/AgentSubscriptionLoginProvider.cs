using System.Diagnostics;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Runs an agent CLI's subscription sign-in and drives it to completion.
/// <para>
/// Unlike the output-scraping version this replaces, the flow is declared up front by
/// <see cref="AgentLoginFlowProvider"/>, stdin stays open so a CLI that asks for a pasted code
/// can be answered, and every phase is bounded by a timeout so a CLI that stops talking fails
/// the sign-in instead of hanging it forever.
/// </para>
/// </summary>
public sealed class AgentSubscriptionLoginProvider(
    AgentLoginCommandProvider commands,
    AgentLoginFlowProvider flows,
    AgentLoginCallbackRelay callbacks,
    IAgentProcessEnvironmentProvider environment,
    ILogger<AgentSubscriptionLoginProvider> logger) : IAgentSubscriptionLoginProvider
{
    private static readonly TimeSpan OutputIdle = TimeSpan.FromMilliseconds(250);

    public async Task<AgentSubscriptionLoginResult> LoginAsync(
        AgentKind kind,
        AgentCommand acpCommand,
        string methodId,
        Action<AgentLoginChallengeDto> onChallenge,
        AgentLoginInbox inbox,
        CancellationToken cancellationToken)
    {
        AgentLoginCommand login;
        AgentLoginFlow flow;
        try
        {
            login = commands.Resolve(kind, acpCommand, methodId);
            flow = flows.Resolve(kind);
        }
        catch (InvalidOperationException exception)
        {
            return AgentSubscriptionLoginResult.Failed(exception.Message);
        }

        var start = new ProcessStartInfo(login.FileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in login.Arguments)
            start.ArgumentList.Add(argument);
        foreach (var pair in environment.EnvironmentFor(kind))
            start.Environment[pair.Key] = pair.Value;
        foreach (var pair in login.Environment)
            start.Environment[pair.Key] = pair.Value;

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                return AgentSubscriptionLoginResult.Failed($"Could not start {login.FileName}.");
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return AgentSubscriptionLoginResult.Failed($"Could not start the subscription login CLI '{login.FileName}': {exception.Message}");
        }

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var parser = new AgentSubscriptionLoginParser(flow);
        var challenged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Publish()
        {
            onChallenge(parser.Challenge);
            if (parser.Challenge.Url is not null)
                challenged.TrySetResult();
        }

        var stdout = ReadAsync(process.StandardOutput, parser, Publish, lifetime.Token);
        var stderr = ReadAsync(process.StandardError, parser, Publish, lifetime.Token);
        var submissions = ConsumeAsync(process, parser, inbox, flow, Publish, lifetime.Token);

        try
        {
            await AwaitChallengeAsync(challenged.Task, process, flow, lifetime.Token);
            await AwaitExitAsync(process, flow, lifetime.Token);
        }
        catch (TimeoutException exception)
        {
            await StopAsync(process, lifetime, stdout, stderr, submissions);
            return AgentSubscriptionLoginResult.Failed(exception.Message, parser.Challenge);
        }
        catch (OperationCanceledException)
        {
            await StopAsync(process, lifetime, stdout, stderr, submissions);
            throw;
        }

        await lifetime.CancelAsync();
        await DrainAsync(stdout);
        await DrainAsync(stderr);
        await DrainAsync(submissions);

        if (process.ExitCode != 0)
        {
            logger.LogInformation("Subscription login for {AgentKind} exited with a non-zero status.", kind);
            return AgentSubscriptionLoginResult.Failed(
                $"Subscription login failed with exit code {process.ExitCode}. Open the printed link and finish signing in with your subscription.",
                parser.Challenge);
        }

        return AgentSubscriptionLoginResult.SucceededResult(parser.Challenge, parser.ClaudeOAuthToken);
    }

    /// <summary>
    /// Waits for the CLI to print its sign-in link. A CLI that exits first has failed outright,
    /// and one that simply stops talking is bounded rather than left pending.
    /// </summary>
    private static async Task AwaitChallengeAsync(Task challenged, Process process, AgentLoginFlow flow, CancellationToken cancellationToken)
    {
        var exited = process.WaitForExitAsync(cancellationToken);
        var completed = await Task.WhenAny(challenged, exited, Task.Delay(flow.ChallengeTimeout, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        if (completed == challenged || completed == exited)
            return;
        throw new TimeoutException(
            $"The agent CLI did not print a sign-in link within {flow.ChallengeTimeout.TotalSeconds:0} seconds.");
    }

    private static async Task AwaitExitAsync(Process process, AgentLoginFlow flow, CancellationToken cancellationToken)
    {
        var exited = process.WaitForExitAsync(cancellationToken);
        var completed = await Task.WhenAny(exited, Task.Delay(flow.CompletionTimeout, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        if (completed == exited)
        {
            await exited;
            return;
        }

        throw new TimeoutException(
            $"The sign-in was not completed within {flow.CompletionTimeout.TotalMinutes:0} minutes. Start it again to get a fresh link.");
    }

    private static async Task ReadAsync(
        TextReader reader,
        AgentSubscriptionLoginParser parser,
        Action onChallenge,
        CancellationToken cancellationToken)
    {
        var segments = new AgentLoginOutputReader(reader, OutputIdle);
        await segments.ReadAsync(
            segment =>
            {
                if (parser.Append(segment))
                    onChallenge();
            },
            cancellationToken);
    }

    /// <summary>
    /// Feeds codes and intercepted redirects into the running sign-in: a pasted code goes to the
    /// CLI's stdin, a redirect is replayed against the loopback address the CLI advertised.
    /// </summary>
    private async Task ConsumeAsync(
        Process process,
        AgentSubscriptionLoginParser parser,
        AgentLoginInbox inbox,
        AgentLoginFlow flow,
        Action onChallenge,
        CancellationToken cancellationToken)
    {
        await foreach (var submission in inbox.ReadAllAsync(cancellationToken))
        {
            if (submission.Kind == AgentLoginSubmissionKind.Code && flow.NeedsCodeInput)
            {
                await process.StandardInput.WriteLineAsync(submission.Value.AsMemory(), cancellationToken);
                await process.StandardInput.FlushAsync(cancellationToken);
                onChallenge();
                continue;
            }

            if (submission.Kind != AgentLoginSubmissionKind.Callback)
                continue;

            var redirect = parser.Challenge.RedirectUri;
            if (redirect is null)
            {
                logger.LogInformation("A sign-in redirect was posted before the agent CLI advertised a callback address.");
                continue;
            }

            if (!await callbacks.RelayAsync(redirect, submission.Value, cancellationToken))
                logger.LogInformation("A posted sign-in redirect did not match the agent CLI's callback address and was refused.");
        }
    }

    private static async Task StopAsync(Process process, CancellationTokenSource lifetime, params Task[] readers)
    {
        await lifetime.CancelAsync();
        foreach (var reader in readers)
            await DrainAsync(reader);
        TryStop(process);
    }

    private static async Task DrainAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            return;
        }
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return;
        }
    }
}
