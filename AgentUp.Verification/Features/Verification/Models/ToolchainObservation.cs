namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// What a toolchain probe found. <paramref name="Version"/> is null when the tool is absent
/// or its version text could not be read.
/// </summary>
public sealed record ToolchainObservation(bool Present, string? Version)
{
    public static ToolchainObservation Missing { get; } = new(false, null);
}
