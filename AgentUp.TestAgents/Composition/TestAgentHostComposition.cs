using AgentUp.TestAgents.Features.Acp.Controllers;
using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Controllers;
using AgentUp.TestAgents.Features.Authentication.Services;
using AgentUp.TestAgents.Features.Host.Controllers;
using AgentUp.TestAgents.Features.Host.Services;
using AgentUp.TestAgents.Features.IdentityProvider.Controllers;
using AgentUp.TestAgents.Features.IdentityProvider.Services;

namespace AgentUp.TestAgents.Composition;

/// <summary>
/// The composition root: the one place that wires the slices together, so the entrypoint sees only
/// a controller and no slice sees another's internals.
/// </summary>
public static class TestAgentHostComposition
{
    public static TestAgentHostController Host() => new(
        new TestAgentHostService(
            new AcpController(new AcpAgentService()),
            new AuthenticationController(new TestAgentSignInService()),
            new IdentityProviderController(new IdentityProviderHostService())));
}
