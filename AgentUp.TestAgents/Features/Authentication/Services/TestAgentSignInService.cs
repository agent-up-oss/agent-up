using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Authentication.Providers;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Authentication.Services;

/// <summary>
/// Owns the sign-in flows and the credential store, so the slice's controller routes to a service
/// rather than reaching for providers itself.
/// </summary>
/// <param name="home">
/// Where credentials are kept. Passed in rather than read from the environment, so a test can
/// point an agent at a directory of its own instead of the account running it.
/// </param>
public sealed class TestAgentSignInService(string? home = null)
{
    public ITestAgentCredentialStore Credentials(TestAgentSchema schema) => new TestAgentCredentialStore(schema, home);

    /// <summary>The sign-in this agent implements, as its own flow.</summary>
    public ITestAgentLoginFlow Flow(TestAgentSchema schema, HttpClient client, string identityProviderUrl) =>
        TestAgentLoginFlowFactory.Create(schema, client, identityProviderUrl);

    /// <summary>Runs the sign-in this agent implements, returning its token or null on failure.</summary>
    public Task<string?> SignInAsync(
        TestAgentSchema schema,
        HttpClient client,
        string identityProviderUrl,
        TextWriter output,
        TextReader input,
        CancellationToken cancellationToken) =>
        Flow(schema, client, identityProviderUrl).RunAsync(output, input, cancellationToken);
}
