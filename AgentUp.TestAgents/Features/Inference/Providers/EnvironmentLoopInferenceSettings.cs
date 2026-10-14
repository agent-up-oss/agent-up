using AgentUp.TestAgents.Features.Inference.Interfaces;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>
/// Reads the printed loop-inference seed from the environment. Missing or unparsable
/// values become 42 so a run is always replayable from the job log.
/// </summary>
public sealed class EnvironmentLoopInferenceSettings : ILoopInferenceSettings
{
    public const uint DefaultSeed = 42;

    public uint Seed
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("AGENTUP_LOOP_INFERENCE_SEED");
            return uint.TryParse(raw, out var seed) ? seed : DefaultSeed;
        }
    }
}
