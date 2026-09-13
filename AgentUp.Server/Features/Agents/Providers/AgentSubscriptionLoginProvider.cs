using System.Diagnostics;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentSubscriptionLoginProvider(
    AgentLoginCommandProvider commands,
    IAgentProcessEnvironmentProvider environment,
    ILogger<AgentSubscriptionLoginProvider> logger) : IAgentSubscriptionLoginProvider
{
    public async Task<AgentSubscriptionLoginResult> LoginAsync(
        AgentKind kind,
        AgentCommand acpCommand,
        string methodId,
        Action<AgentLoginChallengeDto> onChallenge,
        CancellationToken cancellationToken)
    {
        AgentLoginCommand login;
        try
        {
            login = commands.Resolve(kind, acpCommand, methodId);
        }
        catch (InvalidOperationException exception)
        {
            return AgentSubscriptionLoginResult.Failed(exception.Message);
        }

        var start = new ProcessStartInfo(login.FileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

        var parser = new AgentSubscriptionLoginParser();
        var stdout = ReadAsync(process.StandardOutput, parser, onChallenge, cancellationToken);
        var stderr = ReadAsync(process.StandardError, parser, onChallenge, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await DrainAsync(stdout);
            await DrainAsync(stderr);
            TryStop(process);
            throw;
        }

        await Task.WhenAll(stdout, stderr);
        if (process.ExitCode != 0)
        {
            logger.LogInformation("Subscription login for {AgentKind} exited with a non-zero status.", kind);
            return AgentSubscriptionLoginResult.Failed(
                $"Subscription login failed with exit code {process.ExitCode}. Open the printed link and finish signing in with your subscription.",
                parser.Challenge);
        }

        return AgentSubscriptionLoginResult.SucceededResult(parser.Challenge, parser.ClaudeOAuthToken);
    }

    private static async Task ReadAsync(
        StreamReader reader,
        AgentSubscriptionLoginParser parser,
        Action<AgentLoginChallengeDto> onChallenge,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (parser.Append(line))
                onChallenge(parser.Challenge);
        }
    }

    private static async Task DrainAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
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
