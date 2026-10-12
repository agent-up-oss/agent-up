using AgentUp.TestAgents.Features.Acp.Controllers;
using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Controllers;
using AgentUp.TestAgents.Features.Authentication.Services;
using AgentUp.TestAgents.Features.Host.Controllers;
using AgentUp.TestAgents.Features.Host.Models;
using AgentUp.TestAgents.Features.Host.Services;
using AgentUp.TestAgents.Features.IdentityProvider.Controllers;
using AgentUp.TestAgents.Features.IdentityProvider.Services;
using AgentUp.TestAgents.Features.Inference.Controllers;
using AgentUp.TestAgents.Features.Inference.Providers;
using AgentUp.TestAgents.Features.Inference.Services;

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
            new IdentityProviderController(new IdentityProviderHostService()),
            new InferenceAcpController(
                new LocalInferenceAcpService(
                    new LlamaCppTokenGenerator(new GgufModelCache(new HttpDownloader())),
                    new ToolCallParser(),
                    new WorktreeToolExecutor(),
                    new EnvironmentLoopInferenceSettings())),
            new TestAgentConsole(Console.In, Console.Out, Console.Error)));
}
