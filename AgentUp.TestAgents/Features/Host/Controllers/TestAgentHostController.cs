using AgentUp.TestAgents.Features.Host.Models;
using AgentUp.TestAgents.Features.Host.Services;

namespace AgentUp.TestAgents.Features.Host.Controllers;

/// <summary>The host slice's boundary, so the entrypoint calls a controller rather than a service.</summary>
public sealed class TestAgentHostController
{
    public Task<int> RunAsync(TestAgentCommand command, CancellationToken cancellationToken) =>
        new TestAgentHostService().RunAsync(command, cancellationToken);
}
