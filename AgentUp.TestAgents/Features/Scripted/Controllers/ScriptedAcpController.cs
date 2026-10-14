using AgentUp.TestAgents.Features.Scripted.Services;

namespace AgentUp.TestAgents.Features.Scripted.Controllers;

public sealed class ScriptedAcpController(ScriptedAcpService agent)
{
    public Task ServeAsync(TextReader input, TextWriter output, CancellationToken cancellationToken) =>
        agent.RunAsync(input, output, cancellationToken);
}
