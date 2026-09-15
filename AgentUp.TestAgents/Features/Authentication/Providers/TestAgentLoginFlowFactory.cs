using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Authentication.Providers;

/// <summary>Picks the sign-in a given test agent implements.</summary>
public static class TestAgentLoginFlowFactory
{
    public static ITestAgentLoginFlow Create(TestAgentSchema schema, HttpClient client, string identityProviderUrl) => schema switch
    {
        TestAgentSchema.LoopbackRedirect => new LoopbackRedirectLoginFlow(client, identityProviderUrl),
        TestAgentSchema.DeviceCode => new DeviceCodeLoginFlow(client, identityProviderUrl),
        TestAgentSchema.PastedCode => new PastedCodeLoginFlow(client, identityProviderUrl),
        TestAgentSchema.SilentPoll => new SilentPollLoginFlow(client, identityProviderUrl),
        _ => throw new InvalidOperationException($"No sign-in is defined for {schema}.")
    };
}
