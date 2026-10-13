using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Services;

namespace AgentUp.Server.Features.Agents.Controllers;

public sealed class AutonomousLoopController(AutonomousLoopService loop)
{
    public Task<LoopRunResult> RunAsync(LoopRunRequest request, CancellationToken cancellationToken) =>
        loop.RunAsync(request, cancellationToken);

    public IReadOnlyList<string> Invariants(LoopRunResult result) => loop.Invariants(result);
}
