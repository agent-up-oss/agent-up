using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Authentication.Services;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Authentication.Controllers;

/// <summary>The authentication slice's boundary: signing an agent in and keeping its credential.</summary>
public sealed class AuthenticationController(TestAgentSignInService signIn)
{
    public ITestAgentCredentialStore Credentials(TestAgentSchema schema) => signIn.Credentials(schema);

    public Task<string?> SignInAsync(
        TestAgentSchema schema,
        HttpClient client,
        string identityProviderUrl,
        TextWriter output,
        TextReader input,
        CancellationToken cancellationToken,
        string? publicOrigin = null) =>
        signIn.SignInAsync(schema, client, identityProviderUrl, output, input, cancellationToken, publicOrigin);
}
