using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Reports the real machine's platform and CI status.
/// </summary>
public sealed class PlatformCapabilityProvider : IPlatformCapabilityProvider
{
    public string PlatformId { get; } = ResolvePlatformId();

    public bool IsContinuousIntegration { get; } = ResolveContinuousIntegration();

    private static string ResolvePlatformId()
    {
        if (OperatingSystem.IsMacOS())
            return "macos";

        if (OperatingSystem.IsWindows())
            return "windows";

        return "linux";
    }

    private static bool ResolveContinuousIntegration()
        => string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
           || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
}
