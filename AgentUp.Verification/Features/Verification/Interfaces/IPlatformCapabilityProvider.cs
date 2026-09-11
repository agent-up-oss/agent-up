namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>
/// What this machine can run. Injected rather than read statically so platform and CI
/// branches are covered by deterministic tests instead of skipped on the runner.
/// </summary>
public interface IPlatformCapabilityProvider
{
    /// <summary>Platform identifier for this machine: "linux", "macos" or "windows".</summary>
    string PlatformId { get; }

    /// <summary>Whether this process is running on a continuous integration runner.</summary>
    bool IsContinuousIntegration { get; }
}
