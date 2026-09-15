using AgentUp.TestAgents.Features.Acp.Controllers;
using AgentUp.TestAgents.Features.Authentication.Controllers;
using AgentUp.TestAgents.Features.Host.Models;
using AgentUp.TestAgents.Features.IdentityProvider.Controllers;

namespace AgentUp.TestAgents.Features.Host.Services;

/// <summary>
/// Runs whichever verb the process was started for, reaching every other slice through its
/// controller rather than its internals.
/// </summary>
public sealed class TestAgentHostService(
    AcpController acp,
    AuthenticationController authentication,
    IdentityProviderController identityProvider,
    TestAgentConsole console)
{
    public async Task<int> RunAsync(TestAgentCommand command, CancellationToken cancellationToken)
    {
        if (command.Verb == TestAgentVerb.IdentityProvider)
        {
            // The port goes to stdout as the first line so a harness that asked for an ephemeral
            // one can read back the port it actually got, rather than guessing or scanning.
            await identityProvider.ServeAsync(
                command.Port,
                command.PublicOrigin,
                port =>
                {
                    console.Out.WriteLine(port);
                    console.Out.Flush();
                },
                cancellationToken);
            return 0;
        }

        var credentials = authentication.Credentials(command.Schema);
        if (command.Verb == TestAgentVerb.Acp)
        {
            await acp.ServeAsync(command.Schema, credentials, console.In, console.Out, cancellationToken);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(command.IdentityProviderUrl))
        {
            await console.Error.WriteLineAsync(
                "No identity provider was configured. Pass --idp or set AGENTUP_TEST_IDP_URL.");
            return 2;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        try
        {
            var token = await authentication.SignInAsync(
                command.Schema,
                client,
                command.IdentityProviderUrl.TrimEnd('/'),
                console.Out,
                console.In,
                cancellationToken);
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
            await console.Error.WriteLineAsync($"Could not reach the identity provider: {exception.Message}");
            return 3;
        }
    }
}
