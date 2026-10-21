namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// A toolchain a check needs before it can run. <paramref name="MinimumVersion"/> is optional;
/// when set, an older install is the same outcome as a missing one.
/// </summary>
public sealed record ToolchainRequirement(string Id, string? MinimumVersion);
