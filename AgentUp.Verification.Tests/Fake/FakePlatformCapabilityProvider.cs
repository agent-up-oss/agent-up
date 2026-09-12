using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Tests.Fake;

/// <summary>
/// A platform the test chooses. Exists so platform and CI branches are covered
/// deterministically instead of skipped with Assume.That on the real runner.
/// </summary>
internal sealed class FakePlatformCapabilityProvider(string platformId, bool isContinuousIntegration = false)
    : IPlatformCapabilityProvider
{
    public string PlatformId => platformId;

    public bool IsContinuousIntegration => isContinuousIntegration;
}
