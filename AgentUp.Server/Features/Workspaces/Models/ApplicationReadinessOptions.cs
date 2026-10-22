namespace AgentUp.Server.Features.Workspaces.Models;

public sealed record ApplicationReadinessOptions(
    TimeSpan PollInterval,
    TimeSpan Deadline,
    TimeSpan SettleWindow)
{
    public static ApplicationReadinessOptions Production { get; } = new(
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(120),
        TimeSpan.FromSeconds(2));

    public static ApplicationReadinessOptions Immediate { get; } = new(
        TimeSpan.FromMilliseconds(20),
        TimeSpan.FromSeconds(2),
        TimeSpan.Zero);
}
