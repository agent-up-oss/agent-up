using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Interfaces;

/// <summary>Resolves a pinned GGUF onto disk, downloading on a cache miss.</summary>
public interface IGgufModelCache
{
    Task<string> EnsureAsync(GgufModelSpec spec, CancellationToken cancellationToken);
}
