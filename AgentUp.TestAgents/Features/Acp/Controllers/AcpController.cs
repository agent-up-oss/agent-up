using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Acp.Controllers;

/// <summary>The ACP slice's boundary: serving the protocol over stdio.</summary>
public sealed class AcpController(AcpAgentService agent)
{
    public Task ServeAsync(
        TestAgentSchema schema,
        ITestAgentCredentialStore credentials,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken) =>
        agent.RunAsync(schema, credentials, input, output, cancellationToken);
}
