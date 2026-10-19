using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Reports the real machine's platform and CI status.
/// </summary>
public sealed class PlatformCapabilityProvider : IPlatformCapabilityProvider
{
    private readonly IToolchainProbe _probe;
    private readonly Dictionary<string, ToolchainObservation> _cache = new(StringComparer.Ordinal);

    public PlatformCapabilityProvider()
        : this(new ProcessToolchainProbe())
    {
    }

    public PlatformCapabilityProvider(IToolchainProbe probe)
    {
        _probe = probe;
    }

    public string PlatformId { get; } = ResolvePlatformId();

    public bool IsContinuousIntegration { get; } = ResolveContinuousIntegration();

    public ToolchainObservation Toolchain(string toolchainId)
    {
        if (_cache.TryGetValue(toolchainId, out var cached))
            return cached;

        var observation = _probe.Probe(toolchainId);
        _cache[toolchainId] = observation;
        return observation;
    }

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
