namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>One toolchain that kept a check from running, with the version that was wanted.</summary>
public sealed record ToolchainGap(string Id, string? MinimumVersion, string? FoundVersion)
{
    public string Describe()
        => FoundVersion is null
            ? MinimumVersion is null
                ? $"{Id} is not installed"
                : $"{Id} is not installed; {MinimumVersion} is required"
            : $"{Id} {FoundVersion} is installed; {MinimumVersion} is required";
}

/// <summary>Every toolchain gap for one check. The plan carries this beside the skip reason.</summary>
public sealed record ToolchainSkip(IReadOnlyList<ToolchainGap> Gaps)
{
    public string Describe()
        => string.Join("; ", Gaps.Select(gap => gap.Describe()));
}
