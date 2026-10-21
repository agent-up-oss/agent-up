using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Fake;

/// <summary>
/// A platform the test chooses. Exists so platform and CI branches are covered
/// deterministically instead of skipped with Assume.That on the real runner.
/// </summary>
internal sealed class FakePlatformCapabilityProvider(
    string platformId,
    bool isContinuousIntegration = false,
    IReadOnlyDictionary<string, ToolchainObservation>? toolchains = null)
    : IPlatformCapabilityProvider
{
    public string PlatformId => platformId;

    public bool IsContinuousIntegration => isContinuousIntegration;

    public ToolchainObservation Toolchain(string toolchainId)
        => toolchains is not null && toolchains.TryGetValue(toolchainId, out var observation)
            ? observation
            : new ToolchainObservation(true, "1.0.0");
}
