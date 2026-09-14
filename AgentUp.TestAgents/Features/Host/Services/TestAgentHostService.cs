using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Providers;
using AgentUp.TestAgents.Features.Host.Models;
using AgentUp.TestAgents.Features.IdentityProvider.Services;

namespace AgentUp.TestAgents.Features.Host.Services;

/// <summary>Runs whichever verb the process was started for.</summary>
public sealed class TestAgentHostService
{
    public async Task<int> RunAsync(TestAgentCommand command, CancellationToken cancellationToken)
    {
        if (command.Verb == TestAgentVerb.IdentityProvider)
            return await RunIdentityProviderAsync(command, cancellationToken);

        var credentials = new TestAgentCredentialStore(command.Schema);
        if (command.Verb == TestAgentVerb.Acp)
        {
            await new AcpAgentService(command.Schema, credentials)
                .RunAsync(Console.In, Console.Out, cancellationToken);
            return 0;
        }

        return await RunLoginAsync(command, credentials, cancellationToken);
    }

    private static async Task<int> RunIdentityProviderAsync(TestAgentCommand command, CancellationToken cancellationToken)
    {
        await using var provider = new TestIdentityProviderService(command.Port, command.PublicOrigin);
        provider.Start();

        // The port is written to stdout as the first line so a harness that asked for port 0 can
        // read back the one it actually got, rather than guessing or scanning.
        Console.WriteLine(provider.Port);
        Console.Out.Flush();

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Asked to stop.
        }

        return 0;
    }

    private static async Task<int> RunLoginAsync(
        TestAgentCommand command,
        TestAgentCredentialStore credentials,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdentityProviderUrl))
        {
            await Console.Error.WriteLineAsync(
                "No identity provider was configured. Pass --idp or set AGENTUP_TEST_IDP_URL.");
            return 2;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var flow = TestAgentLoginFlowFactory.Create(command.Schema, client, command.IdentityProviderUrl.TrimEnd('/'));

        try
        {
            var token = await flow.RunAsync(Console.Out, Console.In, cancellationToken);
            if (token is null)
                return 1;
            credentials.Write(token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (HttpRequestException exception)
        {
            await Console.Error.WriteLineAsync($"Could not reach the identity provider: {exception.Message}");
            return 3;
        }
    }
}
