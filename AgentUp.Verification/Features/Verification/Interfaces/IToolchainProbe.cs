using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>Looks up one toolchain. Callers cache the answer for the run.</summary>
public interface IToolchainProbe
{
    ToolchainObservation Probe(string toolchainId);

    /// <summary>
    /// Same probe using an explicit PATH so tests do not mutate the process environment.
    /// </summary>
    ToolchainObservation Probe(string toolchainId, string? path);
}
