using AgentUp.TestAgents.Features.Inference.Services;

namespace AgentUp.TestAgents.Features.Inference.Controllers;

/// <summary>The inference slice's boundary: serving locally-inferring ACP over stdio.</summary>
public sealed class InferenceAcpController(LocalInferenceAcpService agent)
{
    public Task ServeAsync(
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        agent.RunAsync(input, output, error, cancellationToken);
}
