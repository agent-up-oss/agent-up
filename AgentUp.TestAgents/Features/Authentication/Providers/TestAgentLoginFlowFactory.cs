using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Authentication.Providers;

/// <summary>Picks the sign-in a given test agent implements.</summary>
public static class TestAgentLoginFlowFactory
{
    /// <param name="publicOrigin">
    /// Where the person reaches the identity provider, when that differs from where the agent
    /// does. Only the links printed for them use it. Device code needs none: the provider mints
    /// its verification link itself, already on the right origin.
    /// </param>
    public static ITestAgentLoginFlow Create(
        TestAgentSchema schema,
        HttpClient client,
        string identityProviderUrl,
        string? publicOrigin = null) => schema switch
    {
        TestAgentSchema.LoopbackRedirect => new LoopbackRedirectLoginFlow(client, identityProviderUrl, publicOrigin),
        TestAgentSchema.DeviceCode => new DeviceCodeLoginFlow(client, identityProviderUrl),
        TestAgentSchema.PastedCode => new PastedCodeLoginFlow(client, identityProviderUrl, publicOrigin),
        TestAgentSchema.SilentPoll => new SilentPollLoginFlow(client, identityProviderUrl),
        _ => throw new InvalidOperationException($"No sign-in is defined for {schema}.")
    };
}
