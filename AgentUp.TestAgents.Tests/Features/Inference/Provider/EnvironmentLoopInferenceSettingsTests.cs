using AgentUp.TestAgents.Features.Inference.Providers;

namespace AgentUp.TestAgents.Tests.Features.Inference.Provider;

[TestFixture]
public sealed class EnvironmentLoopInferenceSettingsTests
{
    [TearDown]
    public void ClearSeed() => Environment.SetEnvironmentVariable("AGENTUP_LOOP_INFERENCE_SEED", null);

    [Test]
    public void Seed_defaultsToFortyTwoWhenUnset()
    {
        Environment.SetEnvironmentVariable("AGENTUP_LOOP_INFERENCE_SEED", null);

        Assert.That(new EnvironmentLoopInferenceSettings().Seed, Is.EqualTo(42));
    }

    [Test]
    public void Seed_readsThePinnedValueSoAFailureReplays()
    {
        Environment.SetEnvironmentVariable("AGENTUP_LOOP_INFERENCE_SEED", "99");

        Assert.That(new EnvironmentLoopInferenceSettings().Seed, Is.EqualTo(99u));
    }
}
