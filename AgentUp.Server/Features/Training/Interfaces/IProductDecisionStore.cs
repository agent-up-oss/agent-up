using AgentUp.Server.Features.Training.Models;

namespace AgentUp.Server.Features.Training.Interfaces;

/// <summary>
/// Where the decision backlog lives between Server runs. A decision outlives the run that wrote
/// it and the review that judged it: the pair is the point.
/// </summary>
public interface IProductDecisionStore
{
    IReadOnlyList<ProductDecision> Read();

    void Write(IReadOnlyList<ProductDecision> decisions);
}
