namespace AgentUp.TestAgents.Features.Inference.Interfaces;

/// <summary>Produces one completion from a full prompt. Implementations own seed and cancellation.</summary>
public interface ITokenGenerator : IAsyncDisposable
{
    Task<string> CompleteAsync(string prompt, uint seed, CancellationToken cancellationToken);
}
