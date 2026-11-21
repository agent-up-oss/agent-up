namespace AgentUp.Server.Features.Forge.DTOs;

/// <summary>Whether a push or fetch with a workspace's forge credential went through, and why not.</summary>
public sealed record ForgeTransferOutcome(bool Succeeded, string? Error)
{
    public static ForgeTransferOutcome Done() => new(true, null);

    public static ForgeTransferOutcome Refused(string error) => new(false, error);
}
