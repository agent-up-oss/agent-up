using AgentUp.TestAgents.Features.Inference.Interfaces;

namespace AgentUp.TestAgents.Tests.Support;

internal sealed class FixedLoopInferenceSettings(uint seed) : ILoopInferenceSettings
{
    public uint Seed { get; } = seed;
}
