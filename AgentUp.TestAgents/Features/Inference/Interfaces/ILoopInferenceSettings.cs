namespace AgentUp.TestAgents.Features.Inference.Interfaces;

/// <summary>Process settings for a locally-inferring ACP run.</summary>
public interface ILoopInferenceSettings
{
    uint Seed { get; }
}
