using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Authentication.Providers;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Authentication.Controllers;

/// <summary>The authentication slice's boundary: signing an agent in and keeping its credential.</summary>
public sealed class AuthenticationController
{
    public ITestAgentCredentialStore Credentials(TestAgentSchema schema) => new TestAgentCredentialStore(schema);

    /// <summary>Runs the sign-in this agent implements, returning its token or null on failure.</summary>
    public Task<string?> SignInAsync(
        TestAgentSchema schema,
        HttpClient client,
        string identityProviderUrl,
        TextWriter output,
        TextReader input,
        CancellationToken cancellationToken) =>
        TestAgentLoginFlowFactory.Create(schema, client, identityProviderUrl).RunAsync(output, input, cancellationToken);
}
